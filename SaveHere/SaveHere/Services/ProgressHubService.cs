﻿using Microsoft.AspNetCore.SignalR;
using SaveHere.Hubs;
using SaveHere.Models;

namespace SaveHere.Services
{
  public interface IProgressHubService
  {
    Task BroadcastProgressUpdate(DownloadProgress progress);
    Task BroadcastStateChange(int itemId, string newStatus);
  }

  public class ProgressHubService : IProgressHubService
  {
    private readonly IHubContext<ProgressHub> _hubContext;

    public ProgressHubService(IHubContext<ProgressHub> hubContext)
    {
      _hubContext = hubContext;
    }

    public async Task BroadcastProgressUpdate(DownloadProgress progress)
    {
      await _hubContext.Clients.All.SendAsync("DownloadProgressUpdate", progress);
    }

    public async Task BroadcastStateChange(int itemId, string newStatus)
    {
      await _hubContext.Clients.All.SendAsync("DownloadStateChanged", itemId, newStatus);
    }
  }
}
