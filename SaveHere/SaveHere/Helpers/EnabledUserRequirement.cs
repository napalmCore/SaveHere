using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using SaveHere.Models;

namespace SaveHere.Helpers
{
  public class EnabledUserRequirement : IAuthorizationRequirement { }

  public class EnabledUserHandler : AuthorizationHandler<EnabledUserRequirement>
  {
    private readonly UserManager<ApplicationUser> _userManager;

    public EnabledUserHandler(UserManager<ApplicationUser> userManager)
    {
      _userManager = userManager;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EnabledUserRequirement requirement)
    {
      if (context.User?.Identity?.IsAuthenticated != true)
      {
        return;
      }

      var user = await _userManager.GetUserAsync(context.User);
      if (user?.IsEnabled == true)
      {
        context.Succeed(requirement);
      }
    }
  }
}
