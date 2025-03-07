using Microsoft.AspNetCore.Identity;

namespace SaveHere.Models
{
  // Add profile data for application users by adding properties to the ApplicationUser class
  public class ApplicationUser : IdentityUser
  {
    public bool IsEnabled { get; set; } = false;
  }
}
