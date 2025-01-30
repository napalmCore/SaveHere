using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO.Compression;

namespace SaveHere.Services
{
  public interface IWarpPlusService
  {
    IReadOnlyList<string> GetLogs();
    Task<bool> IsRunning();
    Task Start();
    Task Stop();
    Task<string> GetExecutablePath();
    Task<bool> IsInstalled();
    Task DownloadAndInstall();
  }

  public class WarpPlusService : IWarpPlusService
  {
    private readonly ILogger<WarpPlusService> _logger;
    private readonly IProgressHubService _progressHubService;
    private readonly IHttpClientFactory _httpClientFactory;
    private Process? _warpProcess;
    private readonly string _basePath;
    private readonly string _executableName;
    private readonly string _downloadUrl;
    private readonly List<string> _logs = new();
    private readonly object _logLock = new();
    private const string DEFAULT_ARGS = "--gool -b 0.0.0.0:8086";

    public WarpPlusService(
        ILogger<WarpPlusService> logger,
        IProgressHubService progressHubService,
        IHttpClientFactory httpClientFactory)
    {
      _logger = logger;
      _progressHubService = progressHubService;
      _httpClientFactory = httpClientFactory;

      // Set up paths and names based on OS
      _basePath = Path.Combine(Directory.GetCurrentDirectory(), "tools", "warp-plus");
      _executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "warp-plus.exe" : "warp-plus";

      // Set download URL based on OS
      var zipFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
          ? "warp-plus_windows-amd64.zip"
          : "warp-plus_linux-amd64.zip";
      _downloadUrl = $"https://github.com/bepass-org/warp-plus/releases/latest/download/{zipFileName}";

      // Ensure directory exists
      Directory.CreateDirectory(_basePath);
    }

    public Task<string> GetExecutablePath()
    {
      return Task.FromResult(Path.Combine(_basePath, _executableName));
    }

    public Task<bool> IsRunning()
    {
      return Task.FromResult(_warpProcess != null && !_warpProcess.HasExited);
    }

    public async Task<bool> IsInstalled()
    {
      var execPath = await GetExecutablePath();
      return File.Exists(execPath);
    }

    public async Task DownloadAndInstall()
    {
      try
      {
        await LogMessage("Starting download of WARP Plus...");

        // Create HTTP client
        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("curl");

        // Download the zip file
        var zipPath = Path.Combine(_basePath, "warp-plus.zip");
        using (var response = await client.GetAsync(_downloadUrl))
        {
          response.EnsureSuccessStatusCode();
          await using var fileStream = new FileStream(zipPath, FileMode.Create);
          await response.Content.CopyToAsync(fileStream);
        }

        await LogMessage("Download complete. Extracting...");

        // Delete existing executable if it exists
        var execPath = await GetExecutablePath();
        if (File.Exists(execPath))
        {
          File.Delete(execPath);
        }

        // Extract the zip file
        using (var archive = ZipFile.OpenRead(zipPath))
        {
          foreach (var entry in archive.Entries)
          {
            if (entry.Name.EndsWith(_executableName, StringComparison.OrdinalIgnoreCase))
            {
              entry.ExtractToFile(execPath, true);
              break;
            }
          }
        }

        // Clean up zip file
        File.Delete(zipPath);

        // Set executable permissions on Unix systems
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
          File.SetUnixFileMode(execPath,
              UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
              UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
              UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        await LogMessage("WARP Plus installation complete!");
      }
      catch (Exception ex)
      {
        var errorMessage = $"Failed to download and install WARP Plus: {ex.Message}";
        _logger.LogError(ex, errorMessage);
        await LogMessage(errorMessage);
        throw;
      }
    }

    public async Task Start()
    {
      if (await IsRunning())
      {
        throw new InvalidOperationException("WARP Plus proxy is already running");
      }

      var executablePath = await GetExecutablePath();
      if (!File.Exists(executablePath))
      {
        throw new FileNotFoundException("WARP Plus executable not found. Please download it first.", executablePath);
      }

      var startInfo = new ProcessStartInfo
      {
        FileName = executablePath,
        Arguments = DEFAULT_ARGS,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = _basePath
      };

      _warpProcess = new Process { StartInfo = startInfo };

      _warpProcess.OutputDataReceived += async (sender, e) =>
      {
        if (!string.IsNullOrEmpty(e.Data))
        {
          await LogMessage(e.Data);
        }
      };

      _warpProcess.ErrorDataReceived += async (sender, e) =>
      {
        if (!string.IsNullOrEmpty(e.Data))
        {
          string errorLog = $"Error: {e.Data}";
          await LogMessage(errorLog);
          _logger.LogWarning("WARP Plus error: {Error}", e.Data);
        }
      };

      _warpProcess.Start();
      _warpProcess.BeginOutputReadLine();
      _warpProcess.BeginErrorReadLine();

      await LogMessage("Starting Proxy... Please Wait Until You See This Message: `msg=\"serving proxy\" address=0.0.0.0:8086`");
      await LogMessage("...");
    }

    public async Task Stop()
    {
      if (!await IsRunning())
      {
        return;
      }

      try
      {
        if (_warpProcess != null)
        {
          if (!_warpProcess.HasExited)
          {
            _warpProcess.Kill();
            await _warpProcess.WaitForExitAsync();
          }
          _warpProcess.Dispose();
          _warpProcess = null;
        }

        await LogMessage("WARP Plus proxy stopped");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error stopping WARP Plus proxy");
        throw;
      }
    }

    public IReadOnlyList<string> GetLogs()
    {
      lock (_logLock)
      {
        return _logs.ToList();
      }
    }

    private async Task LogMessage(string message)
    {
      lock (_logLock)
      {
        _logs.Add(message);
      }
      await _progressHubService.BroadcastWarpPlusLogUpdate(message);
    }

  }
}