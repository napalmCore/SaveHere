using SaveHere.Helpers;
using System.Net;

namespace SaveHere.Endpoints
{
  public static class StreamingEndpoints
  {
    public static void MapStreamingEndpoints(this WebApplication app)
    {
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
    }

  }
}