using SaveHere.Services;

namespace SaveHere.Endpoints
{
  public static class UtilityEndpoints
  {
    public static void MapYtdlpEndpoints(this WebApplication app)
    {
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
    }

  }
}