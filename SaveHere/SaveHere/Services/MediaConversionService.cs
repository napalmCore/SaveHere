using SaveHere.Helpers;
using SaveHere.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace SaveHere.Services
{
  public class MediaConversionService
  {
    private readonly DownloadStateService _downloadStateService;
    private readonly ILogger<MediaConversionService> _logger;
    private readonly IProgressHubService _progressHubService;

    private readonly ConcurrentDictionary<int, MediaConversionItem> _conversionItems = new();

    private static int _nextId = 1;

    public MediaConversionService(
        DownloadStateService downloadStateService,
        ILogger<MediaConversionService> logger,
        IProgressHubService progressHubService)
    {
      _downloadStateService = downloadStateService;
      _logger = logger;
      _progressHubService = progressHubService;
    }

    public Task<List<MediaConversionItem>> GetConversionItemsAsync()
    {
      return Task.FromResult(_conversionItems.Values.ToList());
    }

    public Task<MediaConversionItem?> GetConversionItemByIdAsync(int id)
    {
      _conversionItems.TryGetValue(id, out var item);
      return Task.FromResult(item);
    }

    public Task<MediaConversionItem> AddConversionItemAsync(string inputFile, string outputFormat, string? customOptions)
    {
      if (string.IsNullOrWhiteSpace(inputFile))
      {
        throw new ArgumentException("Input file cannot be empty", nameof(inputFile));
      }

      var item = new MediaConversionItem
      {
        Id = Interlocked.Increment(ref _nextId), // Get the next ID
        InputFile = inputFile,
        OutputFormat = outputFormat,
        CustomOptions = customOptions,
        Status = EQueueItemStatus.Paused
      };

      _conversionItems.TryAdd(item.Id, item); // Add to dictionary

      return Task.FromResult(item);
    }

    public Task UpdateItemStateAsync(int itemId, EQueueItemStatus newStatus)
    {
      if (_conversionItems.TryGetValue(itemId, out var item))
      {
        item.Status = newStatus;
      }
      return Task.CompletedTask;
    }

    public Task DeleteConversionItemAsync(int id)
    {
      if (_conversionItems.TryRemove(id, out _))
      {
        _downloadStateService.RemoveTokenSource(id);
      }
      return Task.CompletedTask;
    }

    public async Task CancelConversionAsync(int id)
    {
      var item = await GetConversionItemByIdAsync(id);
      if (item == null) return;
      if (item.Status == EQueueItemStatus.Cancelled) return;

      item.Status = EQueueItemStatus.Cancelled;
      await UpdateItemStateAsync(item.Id, EQueueItemStatus.Cancelled);
      await _progressHubService.BroadcastStateChange(id, item.Status.ToString());

      var tokenSource = _downloadStateService.GetOrAddTokenSource(id);
      tokenSource.Cancel();
    }

    public async Task StartConversionAsync(MediaConversionItem item)
    {
      if (item == null) throw new Exception("Item not found");
      if (item.Status == EQueueItemStatus.Downloading) throw new Exception("Conversion is already in progress");

      var tokenSource = _downloadStateService.GetOrAddTokenSource(item.Id);
      var token = tokenSource.Token;

      try
      {
        // Clear previous logs when starting a new conversion
        item.OutputLog.Clear();
        item.PersistedLog = string.Empty;
        item.Status = EQueueItemStatus.Downloading;
        await UpdateItemStateAsync(item.Id, EQueueItemStatus.Downloading);
        await _progressHubService.BroadcastStateChange(item.Id, item.Status.ToString());

        // Get input and output file paths
        var inputPath = Path.Combine(DirectoryBrowser.DownloadsPath, item.InputFile);
        var outputFileName = Path.ChangeExtension(item.InputFile, item.OutputFormat.TrimStart('.'));
        var outputPath = Path.Combine(DirectoryBrowser.DownloadsPath, outputFileName);

        // Build FFmpeg command
        var ffmpegArgs = $"-i \"{inputPath}\" {item.CustomOptions ?? string.Empty} \"{outputPath}\"";

        var startInfo = new ProcessStartInfo
        {
          FileName = "ffmpeg",
          Arguments = ffmpegArgs,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          UseShellExecute = false,
          CreateNoWindow = true
        };

        await _progressHubService.BroadcastLogUpdate(item.Id, $"Starting conversion with command: ffmpeg {ffmpegArgs}");

        using var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += async (sender, e) =>
        {
          if (!string.IsNullOrEmpty(e.Data))
          {
            await _progressHubService.BroadcastLogUpdate(item.Id, e.Data);
          }
        };

        process.ErrorDataReceived += async (sender, e) =>
        {
          if (!string.IsNullOrEmpty(e.Data))
          {
            await _progressHubService.BroadcastLogUpdate(item.Id, e.Data);
          }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(token);

        if (process.ExitCode != 0)
        {
          string exitCodeError = $"FFmpeg process exited with code {process.ExitCode}";
          await _progressHubService.BroadcastLogUpdate(item.Id, exitCodeError);
          throw new Exception($"FFmpeg exited with code {process.ExitCode}");
        }

        item.Status = EQueueItemStatus.Finished;
        item.PersistedLog = string.Join(Environment.NewLine, item.OutputLog);
        await UpdateItemStateAsync(item.Id, EQueueItemStatus.Finished);
        await _progressHubService.BroadcastStateChange(item.Id, item.Status.ToString());
        await _progressHubService.BroadcastLogUpdate(item.Id, "Conversion completed successfully!");
      }
      catch (OperationCanceledException)
      {
        item.Status = EQueueItemStatus.Cancelled;
        item.PersistedLog = string.Join(Environment.NewLine, item.OutputLog);
        await UpdateItemStateAsync(item.Id, EQueueItemStatus.Cancelled);
        await _progressHubService.BroadcastStateChange(item.Id, item.Status.ToString());
        await _progressHubService.BroadcastLogUpdate(item.Id, "Conversion was cancelled.");
        throw;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error during conversion for item {id}: {message}", item.Id, ex.Message);
        item.Status = EQueueItemStatus.Paused;
        item.PersistedLog = string.Join(Environment.NewLine, item.OutputLog);
        await UpdateItemStateAsync(item.Id, EQueueItemStatus.Paused);
        await _progressHubService.BroadcastStateChange(item.Id, item.Status.ToString());
        await _progressHubService.BroadcastLogUpdate(item.Id, $"Conversion failed: {ex.Message}");
        throw;
      }
      finally
      {
        _downloadStateService.RemoveTokenSource(item.Id);
      }
    }

    public Task AppendLogAsync(int itemId, string logLine)
    {
      if (_conversionItems.TryGetValue(itemId, out var item))
      {
        item.OutputLog.Add(logLine); // Directly modify the in-memory list
      }
      return Task.CompletedTask;
    }

  }
}
