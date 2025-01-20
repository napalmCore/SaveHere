namespace SaveHere.Services
{
  public class YtdlpUpdateService : IHostedService, IAsyncDisposable
  {
    private readonly IYtdlpService _ytdlpService;
    private readonly ILogger<YtdlpUpdateService> _logger;
    private Timer? _timer;
    private volatile bool _isRunning;

    public YtdlpUpdateService(
        IYtdlpService ytdlpService,
        ILogger<YtdlpUpdateService> logger)
    {
      _ytdlpService = ytdlpService;
      _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
      _logger.LogInformation("YtdlpUpdate Service is starting");
      _timer = new Timer(DoCheckForUpdates, null, TimeSpan.Zero, TimeSpan.FromDays(1));
      return Task.CompletedTask;
    }

    private void DoCheckForUpdates(object? state)
    {
      // Prevent concurrent executions
      if (_isRunning) return;
      _isRunning = true;

      Task.Run(async () =>
      {
        try
        {
          _logger.LogInformation("Checking for yt-dlp updates");

          if (await _ytdlpService.IsUpdateAvailable())
          {
            _logger.LogInformation("Update available for yt-dlp, downloading new version");
            await _ytdlpService.DownloadLatestVersion();
          }
          else
          {
            _logger.LogInformation("yt-dlp is up to date");
          }
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error occurred while checking for yt-dlp updates");
        }
        finally
        {
          _isRunning = false;
        }
      });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
      _logger.LogInformation("YtdlpUpdate Service is stopping");
      _timer?.Change(Timeout.Infinite, 0);
      return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
      if (_timer is IAsyncDisposable timer)
      {
        await timer.DisposeAsync();
      }
      else
      {
        _timer?.Dispose();
      }

      _timer = null;
    }
  }
}
