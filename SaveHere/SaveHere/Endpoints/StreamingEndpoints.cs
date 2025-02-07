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
        try
        {
          // Decode the filename from the URL
          var decodedFilename = WebUtility.UrlDecode(filename);

          // Combine the decoded filename with the downloads directory path
          var filePath = Path.Combine(DirectoryBrowser.DownloadsPath, decodedFilename);

          // Ensure the file path resolves to the intended downloads directory
          var fullFilePath = Path.GetFullPath(filePath); // Canonical path resolution
          var allowedBasePath = Path.GetFullPath(DirectoryBrowser.DownloadsPath); // Canonical base directory

          // Check if the resolved file path starts with the base directory
          if (!fullFilePath.StartsWith(allowedBasePath, StringComparison.Ordinal))
          {
            // Return an error for path traversal attempt
            return Results.Json(new { error = "Invalid file path." }, statusCode: 400);
          }

          // Check if the file exists
          if (!File.Exists(fullFilePath)) return Results.NotFound();

          var fileInfo = new FileInfo(fullFilePath);
          var extension = Path.GetExtension(decodedFilename).ToLowerInvariant();

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
            var fs = new FileStream(fullFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Results.Stream(fs, contentType);
          }

          // Parse the range header
          var range = rangeHeader.Replace("bytes=", "").Split('-');

          // Validate range
          if (!long.TryParse(range[0], out var start) || start < 0 || start >= fileInfo.Length)
          {
            return Results.Json(new { error = "Invalid range specified." }, statusCode: 416); // 416: Range Not Satisfiable
          }
          var end = range.Length > 1 && long.TryParse(range[1], out var tempEnd) && tempEnd >= start && tempEnd < fileInfo.Length
                    ? tempEnd
                    : fileInfo.Length - 1;

          var length = end - start + 1;

          // Set response headers for partial content
          context.Response.StatusCode = StatusCodes.Status206PartialContent;
          context.Response.Headers.Append("Content-Range", $"bytes {start}-{end}/{fileInfo.Length}");
          context.Response.ContentLength = length;

          // Create a file stream for the requested range
          var fileStream = new FileStream(fullFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
          fileStream.Seek(start, SeekOrigin.Begin);

          // Create a limited stream that will only read the requested range
          var limitedStream = new LimitedStream(fileStream, length);
          return Results.Stream(limitedStream, contentType);
        }
        catch (Exception ex)
        {
          // Catch-all handler for unexpected exceptions
          return Results.Json(new { error = $"An error occurred: {ex.Message}" }, statusCode: 500);
        }
      });
    }
  }
}