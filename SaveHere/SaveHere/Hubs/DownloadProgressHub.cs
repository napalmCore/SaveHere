using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SaveHere.Hubs
{
  [Authorize(Policy = "EnabledUser")]
  public class ProgressHub : Hub
  {
  }
}
