using Microsoft.AspNetCore.SignalR;
using SaveHere.Hubs;
using SaveHere.Models;

namespace SaveHere.Services
{
  public interface IProgressHubService
  {
    Task BroadcastProgressUpdate(DownloadProgress progress);
  }

  public class ProgressHubService : IProgressHubService
  {
    private readonly IHubContext<DownloadProgressHub> _hubContext;

    public ProgressHubService(IHubContext<DownloadProgressHub> hubContext)
    {
      _hubContext = hubContext;
    }

    public async Task BroadcastProgressUpdate(DownloadProgress progress)
    {
      await _hubContext.Clients.All.SendAsync("DownloadProgressUpdate", progress);
    }

  }
}
