using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using SaveHere.Components;
using SaveHere.Components.Account;
using SaveHere.Helpers;
using SaveHere.Hubs;
using SaveHere.Models;
using SaveHere.Models.db;
using SaveHere.Services;

namespace SaveHere
{
  public class Program
  {
    public static void Main(string[] args)
    {
      var builder = WebApplication.CreateBuilder(args);

      // Initializing the database
      var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "db");
      if (!Directory.Exists(dbPath))
      {
        try
        {
          Directory.CreateDirectory(dbPath);
        }
        catch
        {
          throw new InvalidOperationException("Could not create the database directory.");
        }
      }
      builder.Services.AddDbContextFactory<AppDbContext>(options =>
          options.UseSqlite($"Data Source={Path.Combine(dbPath, "database.sqlite3.db")}")
      );

      // Creating the download directory
      DirectoryBrowser.InitializeDownloadsDirectory();

      // Add MudBlazor services
      builder.Services.AddMudServices();

      // Add services to the container.
      builder.Services.AddRazorComponents()
          .AddInteractiveServerComponents()
          .AddInteractiveWebAssemblyComponents();

      builder.Services.AddCascadingAuthenticationState();
      builder.Services.AddScoped<IdentityUserAccessor>();
      builder.Services.AddScoped<IdentityRedirectManager>();
      builder.Services.AddScoped<AuthenticationStateProvider, PersistingRevalidatingAuthenticationStateProvider>();

      builder.Services.AddAuthentication(options =>
          {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
          })
          .AddIdentityCookies();

      builder.Services.AddDatabaseDeveloperPageExceptionFilter();

      builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
          .AddEntityFrameworkStores<AppDbContext>()
          .AddSignInManager()
          .AddDefaultTokenProviders();

      builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

      builder.Services.AddSignalR();

      builder.Services.AddSingleton<IProgressHubService, ProgressHubService>();

      builder.Services.AddSingleton<DownloadStateService>();

      builder.Services.AddScoped<HttpClient>();

      builder.Services.AddScoped<IDownloadQueueService, DownloadQueueService>();

      var app = builder.Build();

      // Configure the HTTP request pipeline.
      if (app.Environment.IsDevelopment())
      {
        app.UseWebAssemblyDebugging();
        app.UseMigrationsEndPoint();
      }
      else
      {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        //app.UseHsts();
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
      }

      //app.UseHttpsRedirection();

      app.UseStaticFiles();
      app.UseAntiforgery();

      app.MapRazorComponents<App>()
          .AddInteractiveServerRenderMode()
          .AddInteractiveWebAssemblyRenderMode()
          .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

      // Add additional endpoints required by the Identity /Account Razor components.
      app.MapAdditionalIdentityEndpoints();

      app.MapHub<ProgressHub>("/DownloadProgressHub");

      app.MapGet("/downloads/{filename}", async (string filename, HttpContext context) =>
      {
        var filePath = Path.Combine(DirectoryBrowser.DownloadsPath, filename);
        if (!File.Exists(filePath)) return Results.NotFound();

        var contentType = "application/octet-stream";
        var fileInfo = new FileInfo(filePath);

        // Set content disposition to attachment to force download
        context.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileInfo.Name}\"");

        var fileStream = new FileStream(
          filePath,
          FileMode.Open,
          FileAccess.Read,
          FileShare.Read
        );

        return Results.Stream(fileStream, contentType, fileInfo.Name);
      });

      // Ensuring the database is created
      using (var scope = app.Services.CreateScope())
      {
        var dbc = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbc.Database.EnsureCreated();
        dbc.Database.Migrate();
      }

      app.Run();
    }
  }
}
