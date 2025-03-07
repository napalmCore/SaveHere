using Microsoft.AspNetCore.Authorization;
using SaveHere.Helpers;
using SaveHere.Services;
using System.Net;

namespace SaveHere.Endpoints
{
  [Authorize(Policy = "EnabledUser")]
  public static class DownloadEndpoints
  {
    public static void MapDownloadEndpoints(this WebApplication app)
    {
      // GET endpoint for downloading files using short links
      app.MapGet("/d/{shortLink}", (string shortLink, HttpContext context, ShortLinkService shortLinkService) =>
      {
        try
        {
          // Resolve the short link to a file path
          if (!shortLinkService.TryGetFilePath(shortLink, out var filePath))
          {
            return Results.NotFound("The requested file was not found.");
          }

          // Ensure the file path resolves to the intended downloads directory
          var fullFilePath = Path.GetFullPath(filePath); // Canonical path resolution
          var allowedBasePath = Path.GetFullPath(DirectoryBrowser.DownloadsPath); // Canonical base directory

          // Check if the resolved file path starts with the base directory
          if (!fullFilePath.StartsWith(allowedBasePath, StringComparison.Ordinal))
          {
            return Results.BadRequest("Invalid file path."); // Path traversal attempt detected
          }

          // Check if the file exists
          if (!File.Exists(fullFilePath)) return Results.NotFound();

          var fileInfo = new FileInfo(fullFilePath);
          var contentType = "application/octet-stream";

          var rangeHeader = context.Request.Headers.Range.ToString();
          context.Response.Headers.Append("Accept-Ranges", "bytes");

          // Set content disposition to attachment to force download
          try
          {
            context.Response.Headers.Append("Content-Disposition", $"attachment; filename= \"{WebUtility.UrlEncode(fileInfo.Name)}\"");
          }
          catch { /* pass for now */ }

          // If no range is specified, return the full file
          if (string.IsNullOrEmpty(rangeHeader))
          {
            context.Response.ContentLength = fileInfo.Length;
            var fs = new FileStream(fullFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Results.Stream(fs, contentType, fileInfo.Name);
          }

          // Parse the range header
          var range = rangeHeader.Replace("bytes=", "").Split('-');

          // Validate range
          if (!long.TryParse(range[0], out var start) || start < 0 || start >= fileInfo.Length)
          {
            return Results.Json(new { error = "Invalid range specified." }, statusCode: 416);
          }

          var end = range.Length > 1 && long.TryParse(range[1], out var tempEnd) && tempEnd >= start && tempEnd < fileInfo.Length
                                    ? tempEnd
                                    : fileInfo.Length - 1;

          var length = end - start + 1;

          // Set response headers for partial content
          context.Response.StatusCode = StatusCodes.Status206PartialContent;
          context.Response.Headers.Append("Content-Range", $"bytes {start}-{end}/{fileInfo.Length}");
          context.Response.ContentLength = length;

          // Create a stream for the requested range
          var fileStream = new FileStream(fullFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
          fileStream.Seek(start, SeekOrigin.Begin);

          // Create a limited stream that will only read the requested range
          var limitedStream = new LimitedStream(fileStream, length);
          return Results.Stream(limitedStream, contentType, fileInfo.Name);
        }
        catch (Exception ex)
        {
          // Return a generic error in case of unexpected issues
          return Results.Json(new { error = $"An error occurred: {ex.Message}" }, statusCode: 500);
        }
      });

    }
  }
}
