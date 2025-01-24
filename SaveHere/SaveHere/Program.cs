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
using System.Net;

namespace SaveHere
{
  public class Program
  {
    public static void Main(string[] args)
    {
      var builder = WebApplication.CreateBuilder(args);

      // Setup logging
      //builder.Logging.ClearProviders();
      //builder.Logging.AddConsole();
      //builder.Logging.SetMinimumLevel(LogLevel.Information);

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

      builder.Services.AddHttpClient();

      builder.Services.AddScoped<IFileManagerService, FileManagerService>();

      builder.Services.AddScoped<IDownloadQueueService, DownloadQueueService>();

      builder.Services.AddSingleton<IYtdlpService, YtdlpService>();

      builder.Services.AddScoped<IYoutubeDownloadQueueService, YoutubeDownloadQueueService>();

      builder.Services.AddHostedService<YtdlpUpdateService>();

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

      // GET endpoint for downloading files
      app.MapGet("/downloads/{filename}", (string filename, HttpContext context) =>
      {
        var filePath = Path.Combine(DirectoryBrowser.DownloadsPath, WebUtility.UrlDecode(filename));
        if (!File.Exists(filePath)) return Results.NotFound();

        var fileInfo = new FileInfo(filePath);
        var contentType = "application/octet-stream";

        var rangeHeader = context.Request.Headers.Range.ToString();
        context.Response.Headers.Append("Accept-Ranges", "bytes");

        // Set content disposition to attachment to force download
        try
        {
          context.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileInfo.Name}\"");
        }
        catch { /*pass for now*/ }

        // If no range is specified, return the full file
        if (string.IsNullOrEmpty(rangeHeader))
        {
          context.Response.ContentLength = fileInfo.Length;
          var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
          return Results.Stream(fs, contentType, fileInfo.Name);
        }

        // Parse the range header
        var range = rangeHeader.Replace("bytes=", "").Split('-');
        var start = long.Parse(range[0]);
        var end = range[1] == "" ? fileInfo.Length - 1 : long.Parse(range[1]);
        var length = end - start + 1;

        // Set response headers for partial content
        context.Response.StatusCode = StatusCodes.Status206PartialContent;
        context.Response.Headers.Append("Content-Range", $"bytes {start}-{end}/{fileInfo.Length}");
        context.Response.ContentLength = length;

        // Create a stream for the requested range
        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        fileStream.Seek(start, SeekOrigin.Begin);

        // Create a limited stream that will only read the requested range
        var limitedStream = new LimitedStream(fileStream, length);
        return Results.Stream(limitedStream, contentType, fileInfo.Name);
      });

      // GET endpoint for streaming media
      app.MapGet("/stream/{filename}", (string filename, HttpContext context) =>
      {
        var filePath = Path.Combine(DirectoryBrowser.DownloadsPath, WebUtility.UrlDecode(filename));
        if (!File.Exists(filePath)) return Results.NotFound();

        var fileInfo = new FileInfo(filePath);
        var extension = Path.GetExtension(filename).ToLowerInvariant();

        // Map common media extensions to MIME types
        var contentType = extension switch
        {
          // Video types
          ".mp4" => "video/mp4",
          ".webm" => "video/webm",
          ".avi" => "video/x-msvideo",
          ".mov" => "video/quicktime",
          ".mkv" => "video/x-matroska",
          // Audio types 
          ".mp3" => "audio/mpeg",
          ".wav" => "audio/wav",
          ".ogg" => "audio/ogg",
          ".m4a" => "audio/mp4",
          ".flac" => "audio/flac",
          ".opus" => "audio/opus",
          _ => "application/octet-stream"
        };

        var rangeHeader = context.Request.Headers.Range.ToString();
        context.Response.Headers.Append("Accept-Ranges", "bytes");

        // If no range is specified, return the full file
        if (string.IsNullOrEmpty(rangeHeader))
        {
          context.Response.ContentLength = fileInfo.Length;
          var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
          return Results.Stream(fs, contentType);
        }

        // Parse the range header
        var range = rangeHeader.Replace("bytes=", "").Split('-');
        var start = long.Parse(range[0]);
        var end = range[1] == "" ? fileInfo.Length - 1 : long.Parse(range[1]);
        var length = end - start + 1;

        // Set response headers for partial content
        context.Response.StatusCode = StatusCodes.Status206PartialContent;
        context.Response.Headers.Append("Content-Range", $"bytes {start}-{end}/{fileInfo.Length}");
        context.Response.ContentLength = length;

        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        fileStream.Seek(start, SeekOrigin.Begin);

        var limitedStream = new LimitedStream(fileStream, length);
        return Results.Stream(limitedStream, contentType);
      });

      // GET endpoint for yt-dlp documentation
      app.MapGet("/ytdlp/supportedsites.md", async (HttpContext context) =>
      {
        var ytdlpService = context.RequestServices.GetRequiredService<IYtdlpService>();
        var filePath = await ytdlpService.GetSupportedSitesFilePath();

        if (!File.Exists(filePath))
        {
          try
          {
            await ytdlpService.EnsureYtdlpAvailable();
          }
          catch { /*pass for now*/ }
          if (!File.Exists(filePath)) return Results.NotFound();
        }

        return Results.File(filePath, "text/markdown");
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
