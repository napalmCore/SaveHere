using SaveHere.Models;

namespace SaveHere.Services
{
  public class SimpleVersionCheckerService : BackgroundService
  {
    private readonly ILogger<SimpleVersionCheckerService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly VersionState _versionState;

    // Check every 12 hours to respect github rate limits
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(12);

    public SimpleVersionCheckerService(
        ILogger<SimpleVersionCheckerService> logger,
        IHttpClientFactory httpClientFactory,
        VersionState versionState)
    {
      _logger = logger;
      _httpClientFactory = httpClientFactory;
      _versionState = versionState;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          await CheckForUpdates(stoppingToken);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error checking for updates");
        }

        await Task.Delay(_checkInterval, stoppingToken);
      }
    }

    private async Task CheckForUpdates(CancellationToken stoppingToken)
    {
      var client = _httpClientFactory.CreateClient();
      client.DefaultRequestHeaders.UserAgent.ParseAdd("curl");

      // Get current version
      var currentVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
      if (currentVersion == null) return;

      try
      {
        // Get version from the csproj file in the latest branch
        // This URL gets the raw content of the project file
        var response = await client.GetStringAsync(
            "https://raw.githubusercontent.com/gudarzi/SaveHere/refs/heads/v3.0/SaveHere/SaveHere/SaveHere.csproj",
            stoppingToken
        );

        // Extract version using simple string search to avoid loading full XML
        var versionStart = response.IndexOf("Version>");
        if (versionStart != -1)
        {
          versionStart = response.IndexOf(">", versionStart) + 1;
          var versionEnd = response.IndexOf("</", versionStart);
          var versionString = response.Substring(versionStart, versionEnd - versionStart);

          if (Version.TryParse(versionString, out Version? latestVersion) && latestVersion > currentVersion)
          {
            _versionState.UpdateAvailable = true;
            _versionState.LatestVersion = latestVersion.ToString();
            _logger.LogInformation("New version {Version} available", latestVersion);
          }
        }
      }
      catch (HttpRequestException ex)
      {
        _logger.LogError(ex, "Failed to check for updates");
      }
    }
  }
}