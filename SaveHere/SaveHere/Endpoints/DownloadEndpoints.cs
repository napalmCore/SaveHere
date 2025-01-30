using SaveHere.Helpers;
using System.Net;

namespace SaveHere.Endpoints
{
  public static class DownloadEndpoints
  {
    public static void MapDownloadEndpoints(this WebApplication app)
    {
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
    }

  }
}