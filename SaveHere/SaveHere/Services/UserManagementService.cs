using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaveHere.Models;
using SaveHere.Models.db;

namespace SaveHere.Services
{
  public interface IUserManagementService
  {
    Task<bool> IsRegistrationEnabledAsync();
    Task<IEnumerable<ApplicationUser>> GetPendingUsersAsync();
    Task<IEnumerable<ApplicationUser>> GetAllUsersAsync();
    Task<bool> EnableUserAsync(string userId);
    Task<bool> DisableUserAsync(string userId);
    Task<bool> SetRegistrationEnabledAsync(bool enabled);
    Task<bool> IsUserEnabledAsync(string userId);
  }

  public class UserManagementService : IUserManagementService
  {
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        ILogger<UserManagementService> logger)
    {
      _userManager = userManager;
      _context = context;
      _logger = logger;
    }

    public async Task<bool> IsRegistrationEnabledAsync()
    {
      var settings = await _context.RegistrationSettings.FirstOrDefaultAsync();
      return settings?.IsRegistrationEnabled ?? false;
    }

    public async Task<IEnumerable<ApplicationUser>> GetPendingUsersAsync()
    {
      return await _context.Users
          .Where(u => !u.IsEnabled)
          .ToListAsync();
    }

    public async Task<IEnumerable<ApplicationUser>> GetAllUsersAsync()
    {
      return await _context.Users.ToListAsync();
    }

    public async Task<bool> EnableUserAsync(string userId)
    {
      var user = await _userManager.FindByIdAsync(userId);
      if (user == null)
      {
        _logger.LogWarning("User not found: {UserId}", userId);
        return false;
      }

      user.IsEnabled = true;
      var result = await _userManager.UpdateAsync(user);

      if (result.Succeeded)
      {
        _logger.LogInformation("Enabled user: {UserId}", userId);
        return true;
      }

      _logger.LogError("Failed to enable user: {UserId}. Errors: {Errors}",
          userId, string.Join(", ", result.Errors.Select(e => e.Description)));
      return false;
    }

    public async Task<bool> DisableUserAsync(string userId)
    {
      var user = await _userManager.FindByIdAsync(userId);
      if (user == null)
      {
        _logger.LogWarning("User not found: {UserId}", userId);
        return false;
      }

      // Prevent disabling the last admin
      if (await _userManager.IsInRoleAsync(user, "Admin"))
      {
        var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
        if (adminUsers.Count <= 1)
        {
          _logger.LogWarning("Cannot disable the last admin user: {UserId}", userId);
          return false;
        }
      }

      user.IsEnabled = false;
      var result = await _userManager.UpdateAsync(user);

      if (result.Succeeded)
      {
        _logger.LogInformation("Disabled user: {UserId}", userId);
        return true;
      }

      _logger.LogError("Failed to disable user: {UserId}. Errors: {Errors}",
          userId, string.Join(", ", result.Errors.Select(e => e.Description)));
      return false;
    }

    public async Task<bool> SetRegistrationEnabledAsync(bool enabled)
    {
      var settings = await _context.RegistrationSettings.FirstOrDefaultAsync();
      if (settings == null)
      {
        settings = new RegistrationSettings { Id = 1, IsRegistrationEnabled = enabled };
        _context.RegistrationSettings.Add(settings);
      }
      else
      {
        settings.IsRegistrationEnabled = enabled;
        _context.RegistrationSettings.Update(settings);
      }

      try
      {
        await _context.SaveChangesAsync();
        _logger.LogInformation("Registration is now {Status}", enabled ? "enabled" : "disabled");
        return true;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to update registration settings");
        return false;
      }
    }

    public async Task<bool> IsUserEnabledAsync(string userId)
    {
      var user = await _userManager.FindByIdAsync(userId);
      return user?.IsEnabled ?? false;
    }
  }
}
