using System.Text.Json;

namespace SaveHere.Services
{
  public class DefaultCredentialsService
  {
    private readonly string _credentialsPath;
    private readonly ILogger<DefaultCredentialsService> _logger;
    private readonly IWebHostEnvironment _environment;

    private class CredentialsData
    {
      public string Username { get; set; } = "";
      public string Password { get; set; } = "";
      public bool IsDefaultPassword { get; set; } = true;
      public DateTime CreatedAt { get; set; }
    }

    public DefaultCredentialsService(
        IWebHostEnvironment environment,
        ILogger<DefaultCredentialsService> logger)
    {
      _environment = environment;
      _logger = logger;
      _credentialsPath = Path.Combine(environment.ContentRootPath, "admin-credentials.json");
    }

    public async Task SaveCredentials(string username, string password)
    {
      var data = new CredentialsData
      {
        Username = username,
        Password = password,
        IsDefaultPassword = true,
        CreatedAt = DateTime.UtcNow
      };

      await File.WriteAllTextAsync(_credentialsPath, JsonSerializer.Serialize(data, new JsonSerializerOptions
      {
        WriteIndented = true
      }));

      _logger.LogInformation("Admin credentials saved to: {Path}", _credentialsPath);
    }

    public async Task<(string username, string password)?> GetDefaultCredentials()
    {
      if (!File.Exists(_credentialsPath))
      {
        return null;
      }

      try
      {
        var data = JsonSerializer.Deserialize<CredentialsData>(
            await File.ReadAllTextAsync(_credentialsPath));

        if (data?.IsDefaultPassword != true)
        {
          return null;
        }

        return (data.Username, data.Password);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error reading credentials file");
        return null;
      }
    }

    public async Task MarkPasswordChanged()
    {
      if (!File.Exists(_credentialsPath))
      {
        return;
      }

      try
      {
        var data = JsonSerializer.Deserialize<CredentialsData>(
            await File.ReadAllTextAsync(_credentialsPath));

        if (data != null)
        {
          data.IsDefaultPassword = false;
          await File.WriteAllTextAsync(_credentialsPath, JsonSerializer.Serialize(data, new JsonSerializerOptions
          {
            WriteIndented = true
          }));
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating credentials file");
      }
    }

    public bool DefaultCredentialsExist()
    {
      return File.Exists(_credentialsPath);
    }
  }
}
