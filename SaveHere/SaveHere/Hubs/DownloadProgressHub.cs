using Microsoft.AspNetCore.SignalR;
using SaveHere.Models;

namespace SaveHere.Hubs
{
  public class DownloadProgressHub : Hub
  {
    public async Task UpdateProgress(DownloadProgress update)
    {
      await Clients.All.SendAsync("DownloadProgressUpdate", update);
    }
  }
}
