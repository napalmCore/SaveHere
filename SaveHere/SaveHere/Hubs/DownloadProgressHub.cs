using Microsoft.AspNetCore.SignalR;
using SaveHere.Models;
using System.Collections.Concurrent;

namespace SaveHere.Hubs
{
  public class DownloadProgressHub : Hub
  {
    //private static readonly ConcurrentDictionary<string, string> _connectionMap = new();

    //public override async Task OnConnectedAsync()
    //{
    //  _connectionMap.TryAdd(Context.ConnectionId, Context.ConnectionId);
    //  await base.OnConnectedAsync();
    //}

    //public override async Task OnDisconnectedAsync(Exception? exception)
    //{
    //  _connectionMap.TryRemove(Context.ConnectionId, out _);
    //  await base.OnDisconnectedAsync(exception);
    //}

    //public async Task UpdateProgress(DownloadProgress update)
    //{
      // Send to all clients except the sender
      //await Clients.Others.SendAsync("DownloadProgressUpdate", update);

    //  await Clients.All.SendAsync("DownloadProgressUpdate", update);
    //}

    //public async Task JoinDownloadGroup(string downloadId)
    //{
    //  await Groups.AddToGroupAsync(Context.ConnectionId, downloadId);
    //}

    //public async Task LeaveDownloadGroup(string downloadId)
    //{
    //  await Groups.RemoveFromGroupAsync(Context.ConnectionId, downloadId);
    //}

  }
}
