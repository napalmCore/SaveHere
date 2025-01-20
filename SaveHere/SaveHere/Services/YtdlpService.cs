using SaveHere.Helpers;
using SaveHere.Models;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace SaveHere.Services
{
  public interface IYtdlpService
  {
    Task EnsureYtdlpAvailable();
    Task<string> GetLatestVersion();
    Task<bool> IsUpdateAvailable();
    Task DownloadLatestVersion();
    Task<string> GetExecutablePath();
    Task DownloadVideoAsync(string url, IProgress<DownloadProgress> progress, CancellationToken cancellationToken);
    Task DownloadVideo(string url, string quality, string proxy, Action<string> onOutputReceived, CancellationToken cancellationToken);
  }

  public class YtdlpService : IYtdlpService
  {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YtdlpService> _logger;
    private const string GITHUB_API_URL = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
    private readonly string _basePath;
    private readonly string _executableName;

    public YtdlpService(IHttpClientFactory httpClientFactory, ILogger<YtdlpService> logger)
    {
      _httpClientFactory = httpClientFactory;
      _logger = logger;

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

    public async Task DownloadVideoAsync(string url, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
      var executablePath = await GetExecutablePath();
      if (!File.Exists(executablePath))
      {
        throw new FileNotFoundException("yt-dlp executable not found. Please ensure it's properly installed.");
      }

      var startInfo = new ProcessStartInfo
      {
        FileName = executablePath,
        Arguments = $"-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\" --progress --newline \"{url}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = DirectoryBrowser.DownloadsPath
      };

      using var process = new Process { StartInfo = startInfo };
      //var progressData = new DownloadProgress();

      process.OutputDataReceived += (sender, e) =>
      {
        if (string.IsNullOrEmpty(e.Data)) return;

        if (e.Data.Contains("%"))
        {
          // Parse progress information
          var progressLine = e.Data.Trim();
          var percentIndex = progressLine.IndexOf("%");
          if (percentIndex > 0)
          {
            var percentStr = progressLine.Substring(0, percentIndex).Trim();
            if (float.TryParse(percentStr, out float percent))
            {
              //progressData.ProgressPercentage = percent;

              // Try to parse speed
              var speedIndex = progressLine.IndexOf("iB/s");
              if (speedIndex > 0)
              {
                var speedPart = progressLine.Substring(0, speedIndex + 4);
                var lastSpace = speedPart.LastIndexOf(' ');
                if (lastSpace > 0)
                {
                  var speedStr = speedPart.Substring(lastSpace + 1);
                  //progressData.CurrentSpeed = Helpers.ParseSpeed(speedStr);
                }
              }

              //progress.Report(progressData);
            }
          }
        }
      };

      process.ErrorDataReceived += (sender, e) =>
      {
        if (!string.IsNullOrEmpty(e.Data))
        {
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
          throw new Exception($"yt-dlp exited with code {process.ExitCode}");
        }
      }
      catch (OperationCanceledException)
      {
        if (!process.HasExited)
        {
          process.Kill();
        }
        throw;
      }
    }

    public async Task DownloadVideo(string url, string quality, string proxy, Action<string> onOutputReceived, CancellationToken cancellationToken)
    {
      var executablePath = await GetExecutablePath();

      var formatOption = !string.IsNullOrEmpty(quality)
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

      using var process = new Process { StartInfo = startInfo };

      process.OutputDataReceived += (sender, e) =>
      {
        if (!string.IsNullOrEmpty(e.Data))
        {
          onOutputReceived(e.Data);
        }
      };

      process.ErrorDataReceived += (sender, e) =>
      {
        if (!string.IsNullOrEmpty(e.Data))
        {
          onOutputReceived($"Error: {e.Data}");
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
          onOutputReceived($"Process exited with code {process.ExitCode}");
          throw new Exception($"yt-dlp exited with code {process.ExitCode}");
        }

        onOutputReceived("Download completed successfully!");
      }
      catch (OperationCanceledException)
      {
        onOutputReceived("Download was cancelled.");
        if (!process.HasExited)
        {
          process.Kill();
        }
        throw;
      }
    }
  }

  // Classes to deserialize GitHub API response
  public class GitHubRelease
  {
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();
  }

  public class GitHubAsset
  {
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";
  }
}