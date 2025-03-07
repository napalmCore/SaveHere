using Microsoft.AspNetCore.Identity;
using SaveHere.Models;
using System.Security.Cryptography;

namespace SaveHere.Services
{
  public class InitialSetupService : IHostedService
  {
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InitialSetupService> _logger;
    private readonly string _credentialsPath;

    public InitialSetupService(
        IServiceProvider serviceProvider,
        ILogger<InitialSetupService> logger,
        IWebHostEnvironment environment)
    {
      _serviceProvider = serviceProvider;
      _logger = logger;
      _credentialsPath = Path.Combine(environment.ContentRootPath, "admin-credentials.txt");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
      using var scope = _serviceProvider.CreateScope();
      var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
      var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

      // Create roles if they don't exist
      string[] roles = { "Admin", "User" };
      foreach (var role in roles)
      {
        if (!await roleManager.RoleExistsAsync(role))
        {
          await roleManager.CreateAsync(new IdentityRole(role));
          _logger.LogInformation("Created role: {Role}", role);
        }
      }

      // Check if any admin exists
      if (!await AnyAdminExistsAsync(userManager))
      {
        // Generate random credentials
        var adminUsername = $"admin-{GenerateRandomNumber()}";
        var password = GenerateSecurePassword();

        // Create admin user
        var adminUser = new ApplicationUser
        {
          UserName = adminUsername,
          Email = $"{adminUsername}@savehere.local",
          EmailConfirmed = true,
          IsEnabled = true
        };

        var result = await userManager.CreateAsync(adminUser, password);
        if (result.Succeeded)
        {
          await userManager.AddToRoleAsync(adminUser, "Admin");
          await SaveCredentialsToFile(adminUsername, password);
          _logger.LogInformation("Created default admin account");
        }
        else
        {
          _logger.LogError("Failed to create admin account: {Errors}",
              string.Join(", ", result.Errors.Select(e => e.Description)));
        }
      }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<bool> AnyAdminExistsAsync(UserManager<ApplicationUser> userManager)
    {
      return await userManager.GetUsersInRoleAsync("Admin") is { Count: > 0 };
    }

    private async Task SaveCredentialsToFile(string username, string password)
    {
      var content = $"""
                Admin Credentials
                =======================
                Username: {username}
                Password: {password}
                """;

      await File.WriteAllTextAsync(_credentialsPath, content);
      _logger.LogInformation("Admin credentials saved to: {Path}", _credentialsPath);
    }

    private static string GenerateRandomNumber()
    {
      return Random.Shared.Next(100000, 999999).ToString();
    }

    private static string GenerateSecurePassword()
    {
      const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*";
      var rand = new Random(RandomNumberGenerator.GetInt32(int.MaxValue));
      return new string(Enumerable.Repeat(chars, 16)
          .Select(s => s[rand.Next(s.Length)])
          .ToArray());
    }
  }
}
