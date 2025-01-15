using Microsoft.AspNetCore.SignalR;

namespace SaveHere.Hubs
{
  public class ProgressHub : Hub
  {
    public async Task SubscribeToDownload(int itemId)
    {
      await Groups.AddToGroupAsync(Context.ConnectionId, $"download_{itemId}");
    }
  }
}
