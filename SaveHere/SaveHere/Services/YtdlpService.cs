using SaveHere.Helpers;
using SaveHere.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.SignalR;

namespace SaveHere.Services
{
  public interface IYtdlpService
  {
    Task EnsureYtdlpAvailable();
    Task<string> GetLatestVersion();
    Task<bool> IsUpdateAvailable();
    Task DownloadLatestVersion();
    Task<string> GetExecutablePath();
    Task DownloadVideo(int itemId, string url, string quality, string proxy, CancellationToken cancellationToken);
  }

  public class YtdlpService : IYtdlpService
  {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YtdlpService> _logger;
    private const string GITHUB_API_URL = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
    private readonly string _basePath;
    private readonly string _executableName;
    private readonly IProgressHubService _progressHubService;

    public YtdlpService(IHttpClientFactory httpClientFactory, ILogger<YtdlpService> logger, IProgressHubService progressHubService)
    {
      _httpClientFactory = httpClientFactory;
      _logger = logger;
      _progressHubService = progressHubService;

      // Set up base path and executable name based on OS
      _basePath = Path.Combine(Directory.GetCurrentDirectory(), "tools");
      _executableName = GetExecutableNameForCurrentOS();

      // Ensure tools directory exists
      Directory.CreateDirectory(_basePath);
    }

    private string GetExecutableNameForCurrentOS()
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        return "yt-dlp.exe";
      else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        return "yt-dlp_linux";
      else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        return "yt-dlp_macos";
      else
        throw new PlatformNotSupportedException("Current OS is not supported");
    }

    public async Task EnsureYtdlpAvailable()
    {
      try
      {
        string executablePath = await GetExecutablePath();
        if (!File.Exists(executablePath) || await IsUpdateAvailable())
        {
          await DownloadLatestVersion();
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to ensure yt-dlp availability");
        throw;
      }
    }

    public async Task<string> GetLatestVersion()
    {
      try
      {
        using var client = CreateGitHubClient();
        var release = await client.GetFromJsonAsync<GitHubRelease>(GITHUB_API_URL);
        return release?.TagName ?? throw new Exception("Could not get latest version");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get latest version");
        throw;
      }
    }

    public async Task<bool> IsUpdateAvailable()
    {
      try
      {
        var versionFile = Path.Combine(_basePath, "version.txt");

        if (!File.Exists(versionFile))
          return true;

        var currentVersion = await File.ReadAllTextAsync(versionFile);
        var latestVersion = await GetLatestVersion();

        return currentVersion != latestVersion;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to check for updates");
        throw;
      }
    }

    public async Task DownloadLatestVersion()
    {
      try
      {
        using var client = CreateGitHubClient();
        var release = await client.GetFromJsonAsync<GitHubRelease>(GITHUB_API_URL);
        if (release == null) throw new Exception("Could not get release information");

        var asset = release.Assets.FirstOrDefault(a => a.Name == _executableName)
            ?? throw new Exception($"Could not find asset for {_executableName}");

        var executablePath = Path.Combine(_basePath, _executableName);
        var tempPath = executablePath + ".tmp";

        // Download the file
        using (var response = await client.GetAsync(asset.BrowserDownloadUrl))
        using (var fileStream = File.Create(tempPath))
        {
          await response.Content.CopyToAsync(fileStream);
        }

        // Replace old file with new one
        if (File.Exists(executablePath))
          File.Delete(executablePath);

        File.Move(tempPath, executablePath);

        // Set executable permissions on Unix systems
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
          File.SetUnixFileMode(executablePath,
              UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
              UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
              UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        // Save version information
        await File.WriteAllTextAsync(Path.Combine(_basePath, "version.txt"), release.TagName);

        _logger.LogInformation("Successfully downloaded yt-dlp version {Version}", release.TagName);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to download latest version");
        throw;
      }
    }

    public Task<string> GetExecutablePath()
    {
      return Task.FromResult(Path.Combine(_basePath, _executableName));
    }

    private HttpClient CreateGitHubClient()
    {
      var client = _httpClientFactory.CreateClient();
      //client.DefaultRequestHeaders.UserAgent.ParseAdd("SaveHere-App");
      client.DefaultRequestHeaders.UserAgent.ParseAdd("curl");
      return client;
    }

    public async Task DownloadVideo(int itemId, string url, string quality, string proxy, CancellationToken cancellationToken)
    {
      var executablePath = await GetExecutablePath();

      var formatOption = !(string.IsNullOrEmpty(quality) || quality == "Best")
        ? $"-f \"{quality}\""
        : "-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\"";

      var proxyOption = !string.IsNullOrEmpty(proxy)
          ? $"--proxy \"{proxy}\""
          : "";

      var startInfo = new ProcessStartInfo
      {
        FileName = executablePath,
        Arguments = $"{formatOption} {proxyOption} \"{url}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = DirectoryBrowser.DownloadsPath
      };

      await _progressHubService.BroadcastLogUpdate(itemId, $"Running yt-dlp with arguments: {startInfo.Arguments}");

      using var process = new Process { StartInfo = startInfo };

      process.OutputDataReceived += async (sender, e) =>
      {
        if (!string.IsNullOrEmpty(e.Data))
        {
          await _progressHubService.BroadcastLogUpdate(itemId, e.Data);
        }
      };

      process.ErrorDataReceived += async (sender, e) =>
      {
        if (!string.IsNullOrEmpty(e.Data))
        {
          string errorLog = $"Error: {e.Data}";
          await _progressHubService.BroadcastLogUpdate(itemId, errorLog);
          _logger.LogWarning("yt-dlp error: {Error}", e.Data);
        }
      };

      process.Start();
      process.BeginOutputReadLine();
      process.BeginErrorReadLine();

      try
      {
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
          string exitCodeError = $"Process exited with code {process.ExitCode}";
          await _progressHubService.BroadcastLogUpdate(itemId, exitCodeError);
          throw new Exception($"yt-dlp exited with code {process.ExitCode}");
        }

        await _progressHubService.BroadcastLogUpdate(itemId, "Download completed successfully!");
        await _progressHubService.BroadcastStateChange(itemId, EQueueItemStatus.Finished.ToString());
      }
      catch (OperationCanceledException)
      {
        await _progressHubService.BroadcastLogUpdate(itemId, "Download was cancelled.");
        await _progressHubService.BroadcastStateChange(itemId, EQueueItemStatus.Cancelled.ToString());
        if (!process.HasExited)
        {
          process.Kill();
        }
        throw;
      }
      catch (Exception ex)
      {
        string exceptionError = $"Download failed: {ex.Message}";
        await _progressHubService.BroadcastLogUpdate(itemId, exceptionError);
        await _progressHubService.BroadcastStateChange(itemId, EQueueItemStatus.Paused.ToString());
        throw;
      }
    }
  }
}