using Microsoft.EntityFrameworkCore;
using SaveHere.Models;
using SaveHere.Models.db;
using SaveHere.Models.SaveHere.Models;

namespace SaveHere.Services
{
  public interface IYoutubeDownloadQueueService
  {
    Task<List<YoutubeDownloadQueueItem>> GetQueueItemsAsync();
    Task<YoutubeDownloadQueueItem?> GetQueueItemByIdAsync(int id);
    Task<YoutubeDownloadQueueItem> AddQueueItemAsync(string url, string selectedQuality, string proxyUrl);
    Task UpdateQueueItemAsync(YoutubeDownloadQueueItem item);
    Task DeleteQueueItemAsync(int id);
    Task StartDownloadAsync(int id);
    Task CancelDownloadAsync(int id);
    Task AppendLogAsync(int itemId, string logLine);
  }

  public class YoutubeDownloadQueueService : IYoutubeDownloadQueueService
  {
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly DownloadStateService _downloadStateService;
    private readonly ILogger<YoutubeDownloadQueueService> _logger;
    private readonly IProgressHubService _progressHubService;
    private readonly IYtdlpService _ytdlpService;

    public YoutubeDownloadQueueService(
        IDbContextFactory<AppDbContext> contextFactory,
        DownloadStateService downloadStateService,
        ILogger<YoutubeDownloadQueueService> logger,
        IProgressHubService progressHubService,
        IYtdlpService ytdlpService)
    {
      _contextFactory = contextFactory;
      _downloadStateService = downloadStateService;
      _logger = logger;
      _progressHubService = progressHubService;
      _ytdlpService = ytdlpService;
    }

    public async Task<List<YoutubeDownloadQueueItem>> GetQueueItemsAsync()
    {
      await using var context = await _contextFactory.CreateDbContextAsync();
      var items = await context.YoutubeDownloadQueueItems.ToListAsync();
      foreach (var item in items)
      {
        item.OutputLog = item.PersistedLog.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList();
      }
      return items;
    }

    public async Task<YoutubeDownloadQueueItem?> GetQueueItemByIdAsync(int id)
    {
      await using var context = await _contextFactory.CreateDbContextAsync();
      var item = await context.YoutubeDownloadQueueItems.FindAsync(id);
      if (item != null)
      {
        item.OutputLog = item.PersistedLog.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList();
      }
      return item;
    }

    public async Task<YoutubeDownloadQueueItem> AddQueueItemAsync(string url, string selectedQuality, string proxyUrl)
    {
      if (string.IsNullOrWhiteSpace(url))
      {
        throw new ArgumentException("URL cannot be empty", nameof(url));
      }

      if (!Uri.TryCreate(url, UriKind.Absolute, out _))
      {
        throw new ArgumentException("URL is not valid", nameof(url));
      }

      var item = new YoutubeDownloadQueueItem
      {
        Url = url,
        Quality = selectedQuality,
        Proxy = proxyUrl,
        Status = EQueueItemStatus.Paused
      };

      await using var context = await _contextFactory.CreateDbContextAsync();
      context.YoutubeDownloadQueueItems.Add(item);
      await context.SaveChangesAsync();

      return item;
    }

    public async Task UpdateQueueItemAsync(YoutubeDownloadQueueItem item)
    {
      await using var context = await _contextFactory.CreateDbContextAsync();
      context.YoutubeDownloadQueueItems.Update(item);
      await context.SaveChangesAsync();
    }

    public async Task DeleteQueueItemAsync(int id)
    {
      var item = await GetQueueItemByIdAsync(id);
      if (item != null)
      {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.YoutubeDownloadQueueItems.Remove(item);
        _downloadStateService.RemoveTokenSource(id);
        await context.SaveChangesAsync();
      }
    }


    public async Task CancelDownloadAsync(int id)
    {
      var item = await GetQueueItemByIdAsync(id);
      if (item == null) return;
      if (item.Status == EQueueItemStatus.Cancelled) return;

      // Update status immediately
      item.Status = EQueueItemStatus.Cancelled;
      await UpdateQueueItemAsync(item);
      await _progressHubService.BroadcastStateChange(id, item.Status.ToString());

      var tokenSource = _downloadStateService.GetOrAddTokenSource(id);
      tokenSource.Cancel();
    }

    public async Task StartDownloadAsync(int id)
    {
      var item = await GetQueueItemByIdAsync(id);

      if (item == null) throw new Exception("Item not found");
      if (item.Status == EQueueItemStatus.Downloading) throw new Exception("Item is already downloading");

      var tokenSource = _downloadStateService.GetOrAddTokenSource(id);
      var token = tokenSource.Token;

      try
      {
        item.Status = EQueueItemStatus.Downloading;
        await UpdateQueueItemAsync(item);
        await _progressHubService.BroadcastStateChange(id, item.Status.ToString());

        await _ytdlpService.DownloadVideo(item.Id, item.Url, item.Quality, item.Proxy, token);

        item.Status = EQueueItemStatus.Finished;
        await UpdateQueueItemAsync(item);
        await _progressHubService.BroadcastStateChange(id, item.Status.ToString());
      }
      catch (OperationCanceledException)
      {
        item.Status = EQueueItemStatus.Cancelled;
        await UpdateQueueItemAsync(item);
        await _progressHubService.BroadcastStateChange(id, item.Status.ToString());
        throw;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error downloading video for item {id}: {message}", id, ex.Message);
        item.Status = EQueueItemStatus.Paused;
        await UpdateQueueItemAsync(item);
        await _progressHubService.BroadcastStateChange(id, item.Status.ToString());
        throw;
      }
      finally
      {
        _downloadStateService.RemoveTokenSource(id);
      }
    }

    public async Task AppendLogAsync(int itemId, string logLine)
    {
      try
      {
        var item = await GetQueueItemByIdAsync(itemId);
        if (item != null)
        {
          item.PersistedLog += logLine + Environment.NewLine;
          await UpdateQueueItemAsync(item);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error appending log to database for item {itemId}: {Message}", itemId, ex.Message);
      }
    }

  }
}