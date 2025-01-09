using Microsoft.EntityFrameworkCore;
using SaveHere.Models;
using SaveHere.Models.db;
using System.Diagnostics;
using System.Net;

namespace SaveHere.Services
{
  public interface IDownloadQueueService
  {
    Task<List<FileDownloadQueueItem>> GetQueueItemsAsync();
    Task<FileDownloadQueueItem?> GetQueueItemByIdAsync(int id);
    Task<FileDownloadQueueItem> AddQueueItemAsync(string url);
    Task DeleteQueueItemAsync(int id);
    Task<bool> StartDownloadAsync(int id, IProgress<int>? progress = null);
    Task PauseDownloadAsync(int id);
    Task CancelDownloadAsync(int id);
  }

  public class DownloadQueueService : IDownloadQueueService
  {
    private readonly AppDbContext _context;
    private readonly DownloadStateService _downloadStateService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DownloadQueueService> _logger;

    public DownloadQueueService(AppDbContext context, DownloadStateService downloadStateService, HttpClient httpClient, ILogger<DownloadQueueService> logger)
    {
      _context = context;
      _downloadStateService = downloadStateService;
      _httpClient = httpClient;
      _logger = logger;
    }

    public async Task<List<FileDownloadQueueItem>> GetQueueItemsAsync()
    {
      return await _context.FileDownloadQueueItems.ToListAsync();
    }

    public async Task<FileDownloadQueueItem?> GetQueueItemByIdAsync(int id)
    {
      return await _context.FileDownloadQueueItems.FindAsync(id);
    }

    public async Task<FileDownloadQueueItem> AddQueueItemAsync(string url)
    {
      if (string.IsNullOrWhiteSpace(url))
      {
        throw new ArgumentException("URL cannot be empty", nameof(url));
      }

      if (!Uri.TryCreate(url, UriKind.Absolute, out _))
      {
        throw new ArgumentException("URL is not valid", nameof(url));
      }

      var item = new FileDownloadQueueItem
      {
        InputUrl = url,
        Status = EQueueItemStatus.Paused,
        ProgressPercentage = 0
      };

      _context.FileDownloadQueueItems.Add(item);
      await _context.SaveChangesAsync();

      return item;
    }

    public async Task UpdateQueueItemAsync(FileDownloadQueueItem item)
    {
      _context.FileDownloadQueueItems.Update(item);
      await _context.SaveChangesAsync();
    }

    public async Task DeleteQueueItemAsync(int id)
    {
      var item = await GetQueueItemByIdAsync(id);
      if (item != null)
      {
        _context.FileDownloadQueueItems.Remove(item);
        _downloadStateService.RemoveTokenSource(id);
        await _context.SaveChangesAsync();
      }
    }

    public async Task<bool> StartDownloadAsync(int id, IProgress<int>? progress = null)
    {
      var item = await GetQueueItemByIdAsync(id);

      if (item == null) return false;

      if (item.Status == EQueueItemStatus.Downloading) return false;

      var tokenSource = _downloadStateService.GetOrAddTokenSource(id);
      var token = tokenSource.Token;

      try
      {
        item.Status = EQueueItemStatus.Downloading;
        item.ProgressPercentage = 0;
        await UpdateQueueItemAsync(item);

        var downloadSuccess = await DownloadFile(item, progress, token);

        item.Status = downloadSuccess ? EQueueItemStatus.Finished : EQueueItemStatus.Cancelled;
        await UpdateQueueItemAsync(item);

        return downloadSuccess;
      }
      catch (OperationCanceledException)
      {
        item.Status = EQueueItemStatus.Cancelled;
        await UpdateQueueItemAsync(item);
        throw;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error downloading the file for item {id}", id);
        item.Status = EQueueItemStatus.Paused;
        await UpdateQueueItemAsync(item);
        throw;
      }
      finally
      {
        _downloadStateService.RemoveTokenSource(id);
      }
    }

    public async Task PauseDownloadAsync(int id)
    {
      var item = await GetQueueItemByIdAsync(id);
      if (item == null) return;
      if (item.Status != EQueueItemStatus.Downloading) return;
      var tokenSource = _downloadStateService.GetOrAddTokenSource(id);
      tokenSource.Cancel();
    }

    public async Task CancelDownloadAsync(int id)
    {
      var item = await GetQueueItemByIdAsync(id);
      if (item == null) return;
      if (item.Status == EQueueItemStatus.Cancelled) return;
      var tokenSource = _downloadStateService.GetOrAddTokenSource(id);
      tokenSource.Cancel();
    }

    public async Task<bool> DownloadFile(FileDownloadQueueItem queueItem, IProgress<int>? progress, CancellationToken cancellationToken)
    {
      // Validate the URL (must use either HTTP or HTTPS schemes)
      if (string.IsNullOrEmpty(queueItem.InputUrl) ||
          !Uri.TryCreate(queueItem.InputUrl, UriKind.Absolute, out Uri? uriResult) ||
          !(uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
      {
        throw new Exception("Invalid URL");
      }

      try
      {
        var httpClient = _httpClient;

        ProxyServer? proxyServer = null;
        if (proxyServer != null && !(string.IsNullOrWhiteSpace(proxyServer.Protocol) || string.IsNullOrWhiteSpace(proxyServer.Host) || proxyServer.Port == 0))
        {
          var url = proxyServer.Protocol + "://" + proxyServer.Host + ":" + proxyServer.Port;

          var proxy = new WebProxy
          {
            Address = new Uri(url),
            BypassProxyOnLocal = true,
            //Credentials = new NetworkCredential(username, password)
          };

          var httpClientHandler = new HttpClientHandler
          {
            Proxy = proxy,
            UseProxy = true
          };

          httpClient = new HttpClient(httpClientHandler);

        }

        var fileName = Helpers.Helpers.ExtractFileNameFromUrl(queueItem.InputUrl);

        var response = await httpClient.GetAsync(queueItem.InputUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        if (queueItem.bShouldGetFilenameFromHttpHeaders)
        {
          var contentDisposition = response.Content.Headers.ContentDisposition;

          if (contentDisposition != null)
          {
            if (!string.IsNullOrEmpty(contentDisposition.FileNameStar)) fileName = System.Web.HttpUtility.UrlDecode(contentDisposition.FileNameStar.Replace("\"", ""));
            else if (!string.IsNullOrEmpty(contentDisposition.FileName)) fileName = System.Web.HttpUtility.UrlDecode(contentDisposition.FileName.Replace("\"", ""));
          }
        }

        // Ensure the filename is safe by removing invalid characters and making sure it cannot end up being empty
        fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        if (string.IsNullOrWhiteSpace(fileName)) fileName = "unnamed_" + DateTime.Now.ToString("yyyyMMddHHmmss");

        // Try to determine the file extension based on common mime types if the filename doesn't have one already
        if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
        {
          var contentType = response.Content.Headers.ContentType?.MediaType;

          if (contentType != null && Helpers.Helpers.CommonMimeTypes.TryGetValue(contentType, out var extension))
          {
            fileName += extension;
          }
        }

        var downloadPath = Path.Combine(Directory.GetCurrentDirectory(), "downloads");

        // Construct the file path using the base directory and the sanitized filename
        var localFilePath = Path.GetFullPath(Path.Combine(downloadPath, fileName));

        // Ensure the file path is within the intended directory
        if (!localFilePath.StartsWith(Path.GetFullPath(downloadPath), StringComparison.OrdinalIgnoreCase))
        {
          throw new UnauthorizedAccessException("Invalid file path.");
        }

        // Check for existing temp file and corresponding final file
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        string fileExtension = Path.GetExtension(fileName);
        var tempFilePath = localFilePath + ".tmp";
        long totalBytesRead = 0;
        int digit = 1;

        while (System.IO.File.Exists(tempFilePath) || System.IO.File.Exists(localFilePath))
        {
          if (System.IO.File.Exists(tempFilePath))
          {
            totalBytesRead = new FileInfo(tempFilePath).Length;
            break;
          }

          fileName = $"{fileNameWithoutExtension}_{digit}{fileExtension}";
          localFilePath = Path.Combine(downloadPath, fileName);
          tempFilePath = localFilePath + ".tmp";
          digit++;
        }

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, queueItem.InputUrl);

        if (totalBytesRead > 0)
        {
          requestMessage.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(totalBytesRead, null);
        }

        response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        // If the server supports resumption (HTTP 206), do not restart the download from scratch
        bool restartDownload = response.StatusCode != System.Net.HttpStatusCode.PartialContent;

        if (restartDownload)
        {
          totalBytesRead = 0;
        }

        using (var download = await response.Content.ReadAsStreamAsync())
        {
          using var stream = new FileStream(tempFilePath, restartDownload ? FileMode.Create : FileMode.Append, FileAccess.Write);
          var contentLength = response.Content.Headers.ContentLength;

          var buffer = new byte[81920]; // 80KB buffer (default buffer size used by Microsoft's CopyTo method in Stream)
          int bytesRead;
          double elapsedNanoSeconds;
          double elapsedNanoSecondsAvg;
          double bytesPerSecond;
          double bytesPerSecondAvg;

          // To avoid slowing down the process we should not be saving changes to the context on every iteration
          double saveIntervalInSeconds = 0.5; // saving and updating every 0.5 second
          double debounceInSeconds = 0;

          var stopwatch = Stopwatch.StartNew();
          var stopwatchAvg = Stopwatch.StartNew();

          // Ignore progress reporting when the ContentLength's header is not available
          if (!contentLength.HasValue)
          {
            while ((bytesRead = await download.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
            {
              // Check if cancellation is requested
              if (cancellationToken.IsCancellationRequested)
              {
                throw new OperationCanceledException(cancellationToken);
              }

              await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
              totalBytesRead += bytesRead;

              // Calculate download speed
              elapsedNanoSeconds = stopwatch.Elapsed.TotalNanoseconds;
              bytesPerSecond = elapsedNanoSeconds > 0 ? bytesRead / elapsedNanoSeconds * 1e9 : 0;
              elapsedNanoSecondsAvg = stopwatchAvg.Elapsed.TotalNanoseconds;
              bytesPerSecondAvg = elapsedNanoSecondsAvg > 0 ? totalBytesRead / elapsedNanoSecondsAvg * 1e9 : 0;
              stopwatch.Restart();

              // Inform the client at regular intervals
              debounceInSeconds += elapsedNanoSeconds / 1e9;
              if (debounceInSeconds >= saveIntervalInSeconds)
              {
                queueItem.CurrentDownloadSpeed=bytesPerSecond;
                queueItem.AverageDownloadSpeed = bytesPerSecondAvg;
                progress?.Report(queueItem.ProgressPercentage);
                debounceInSeconds = 0;
              }
            }

            queueItem.ProgressPercentage = 100;
            progress?.Report(queueItem.ProgressPercentage);
            await _context.SaveChangesAsync(cancellationToken);
          }
          else
          {
            // Adjusted for correct progress reporting when resuming download
            long totalContentLength = contentLength.Value + totalBytesRead;

            while ((bytesRead = await download.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
            {
              // Check if cancellation is requested
              if (cancellationToken.IsCancellationRequested)
              {
                throw new OperationCanceledException(cancellationToken);
              }

              await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
              totalBytesRead += bytesRead;
              queueItem.ProgressPercentage = (int)(100.0 * totalBytesRead / totalContentLength);
              progress?.Report(queueItem.ProgressPercentage);

              // Calculate download speed
              elapsedNanoSeconds = stopwatch.Elapsed.TotalNanoseconds;
              bytesPerSecond = elapsedNanoSeconds > 0 ? bytesRead / elapsedNanoSeconds * 1e9 : 0;
              elapsedNanoSecondsAvg = stopwatchAvg.Elapsed.TotalNanoseconds;
              bytesPerSecondAvg = elapsedNanoSecondsAvg > 0 ? totalBytesRead / elapsedNanoSecondsAvg * 1e9 : 0;
              stopwatch.Restart();

              // Save progress to the database and inform the client at regular intervals
              debounceInSeconds += elapsedNanoSeconds / 1e9;
              if (debounceInSeconds >= saveIntervalInSeconds)
              {
                queueItem.CurrentDownloadSpeed = bytesPerSecond;
                queueItem.AverageDownloadSpeed = bytesPerSecondAvg;
                await _context.SaveChangesAsync(cancellationToken);
                progress?.Report(queueItem.ProgressPercentage);
                debounceInSeconds = 0;
              }
            }

            // Save any remaining changes
            await _context.SaveChangesAsync(cancellationToken);
          }
        }

        // Rename temp file to final file
        System.IO.File.Move(tempFilePath, localFilePath);

        // Fixing file permissions on linux
        if (OperatingSystem.IsLinux()) System.IO.File.SetUnixFileMode(localFilePath,
            UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead |
            UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite |
            UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
      }
      catch (Exception)
      {
        throw;
      }

      return true;
    }

  }
}
