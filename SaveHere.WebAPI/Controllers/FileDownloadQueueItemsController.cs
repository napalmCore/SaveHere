﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaveHere.WebAPI.DTOs;
using SaveHere.WebAPI.Models;
using SaveHere.WebAPI.Models.db;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace SaveHere.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FileDownloadQueueItemsController : ControllerBase
{
  private readonly AppDbContext _context;
  private readonly HttpClient _httpClient;
  private static ConcurrentDictionary<int, CancellationTokenSource> _cancellationTokenSources = new ConcurrentDictionary<int, CancellationTokenSource>();

  public FileDownloadQueueItemsController(AppDbContext context, HttpClient httpClient)
  {
    _context = context;
    _httpClient = httpClient;
  }

  // GET: api/FileDownloadQueueItems
  [HttpGet]
  public async Task<ActionResult<IEnumerable<FileDownloadQueueItem>>> GetFileDownloadQueueItems()
  {
    return await _context.FileDownloadQueueItems.ToListAsync();
  }

  // GET: api/FileDownloadQueueItems/5
  [HttpGet("{id}")]
  public async Task<ActionResult<FileDownloadQueueItem>> GetFileDownloadQueueItem(int id)
  {
    var fileDownloadQueueItem = await _context.FileDownloadQueueItems.FindAsync(id);

    if (fileDownloadQueueItem == null)
    {
      return NotFound();
    }

    return fileDownloadQueueItem;
  }

  // POST: api/FileDownloadQueueItems
  // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
  [HttpPost]
  public async Task<ActionResult<FileDownloadQueueItem>> PostFileDownloadQueueItem([FromBody] FileDownloadRequestDTO fileDownloadRequest)
  {
    if (!ModelState.IsValid || string.IsNullOrWhiteSpace(fileDownloadRequest.InputUrl))
    {
      return BadRequest();
    }

    var newFileDownload = new FileDownloadQueueItem() { InputUrl = fileDownloadRequest.InputUrl };
    _context.FileDownloadQueueItems.Add(newFileDownload);
    await _context.SaveChangesAsync();

    return CreatedAtAction("GetFileDownloadQueueItem", new { id = newFileDownload.Id }, newFileDownload);
  }

  // DELETE: api/FileDownloadQueueItems/5
  [HttpDelete("{id}")]
  public async Task<IActionResult> DeleteFileDownloadQueueItem(int id)
  {
    var fileDownloadQueueItem = await _context.FileDownloadQueueItems.FindAsync(id);
    if (fileDownloadQueueItem == null)
    {
      return NotFound();
    }

    _context.FileDownloadQueueItems.Remove(fileDownloadQueueItem);
    await _context.SaveChangesAsync();

    return NoContent();
  }

  // POST: api/FileDownloadQueueItems/canceldownload
  [HttpPost("canceldownload")]
  public IActionResult CancelFileDownload([FromBody] FileDownloadCancelRequestDTO request)
  {
    if (!ModelState.IsValid)
    {
      return BadRequest(ModelState);
    }

    try
    {
      // Find the corresponding CancellationTokenSource based on the request ID
      if (_cancellationTokenSources.TryRemove(request.Id, out var cts))
      {
        cts.Cancel();
        return Ok("File download cancelled successfully.");
      }

      return NotFound("Download request not found!");
    }
    catch (Exception ex)
    {
      // Log the exception
      return StatusCode(500, $"An error occurred while cancelling the file download.\n{ex.Message}");
    }
  }

  // POST: api/FileDownloadQueueItems/startdownload
  [HttpPost("startdownload")]
  public async Task<IActionResult> StartFileDownload([FromBody] FileDownloadQueueItemRequestDTO request)
  {
    if (!ModelState.IsValid)
    {
      return BadRequest(ModelState);
    }

    var fileDownloadQueueItem = await _context.FileDownloadQueueItems.FindAsync(request.Id);

    if (fileDownloadQueueItem == null)
    {
      return NotFound();
    }

    fileDownloadQueueItem.Status = EQueueItemStatus.Downloading;
    fileDownloadQueueItem.ProgressPercentage = 0;
    _context.SaveChanges();

    // Create a new CancellationTokenSource instance to manage cancellation for the current download request
    var cts = new CancellationTokenSource();

    if (!_cancellationTokenSources.TryAdd(request.Id, cts))
    {
      return BadRequest("File download has already started!");
    }

    try
    {
      var downloadResult = await DownloadFile(fileDownloadQueueItem, request.UseHeadersForFilename ?? true, cts.Token);

      if (downloadResult)
      {
        fileDownloadQueueItem.Status = EQueueItemStatus.Finished;
        await _context.SaveChangesAsync();
        return Ok("File downloaded successfully.");
      }
      else
      {
        fileDownloadQueueItem.Status = EQueueItemStatus.Paused;
        await _context.SaveChangesAsync();
        return BadRequest("There was an issue in downloading the file!");
      }
    }
    catch (OperationCanceledException)
    {
      fileDownloadQueueItem.Status = EQueueItemStatus.Cancelled;
      await _context.SaveChangesAsync();
      return Ok("File download was cancelled.");
    }
    catch (Exception ex)
    {
      // Log the exception
      return StatusCode(500, $"An error occurred while downloading the file.\n{ex.Message}");
    }
    finally
    {
      _cancellationTokenSources.TryRemove(request.Id, out _);
    }
  }

  [NonAction]
  public async Task<bool> DownloadFile(FileDownloadQueueItem queueItem, bool UseHeadersForFilename, CancellationToken cancellationToken)
  {
    // Validate the URL (must use either HTTP or HTTPS schemes)
    if (string.IsNullOrEmpty(queueItem.InputUrl) ||
        !Uri.TryCreate(queueItem.InputUrl, UriKind.Absolute, out Uri? uriResult) ||
        !(uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
    {
      return false;
    }

    var stopwatch = new Stopwatch();
    var stopwatch2 = new Stopwatch();

    try
    {
      var fileName = Helpers.ExtractFileNameFromUrl(queueItem.InputUrl);

      var response = await _httpClient.GetAsync(queueItem.InputUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

      if (!response.IsSuccessStatusCode) return false;

      if (UseHeadersForFilename)
      {
        var contentDisposition = response.Content.Headers.ContentDisposition;

        if (contentDisposition != null)
        {
          if (!string.IsNullOrEmpty(contentDisposition.FileNameStar)) fileName = System.Web.HttpUtility.UrlDecode(contentDisposition.FileNameStar.Replace("\"", ""));
          else if (!string.IsNullOrEmpty(contentDisposition.FileName)) fileName = System.Web.HttpUtility.UrlDecode(contentDisposition.FileName.Replace("\"", ""));
        }
      }

      // Ensure the filename is safe by removing invalid characters and making sure it cannot end up being empty
      fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
      if (string.IsNullOrWhiteSpace(fileName)) fileName = "unnamed_" + DateTime.Now.ToString("yyyyMMddHHmmss");

      // Try to determine the file extension based on common mime types if the filename doesn't have one already
      if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
      {
        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (contentType != null && Helpers.CommonMimeTypes.TryGetValue(contentType, out var extension))
        {
          fileName += extension;
        }
      }

      // Construct the file path using the base directory and the sanitized filename
      var localFilePath = Path.GetFullPath(Path.Combine("/app/downloads", fileName));

      // Ensure the file path is within the intended directory
      if (!localFilePath.StartsWith(Path.GetFullPath("/app/downloads"), StringComparison.OrdinalIgnoreCase))
      {
        throw new UnauthorizedAccessException("Invalid file path.");
      }

      // Check if a file with the same name already exists
      string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
      string fileExtension = Path.GetExtension(fileName);
      int digit = 1;

      while (System.IO.File.Exists(localFilePath))
      {
        fileName = $"{fileNameWithoutExtension}_{digit}{fileExtension}";
        localFilePath = Path.Combine("/app/downloads", fileName);
        digit++;
      }

      using (var download = await response.Content.ReadAsStreamAsync())
      {
        using var stream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write);
        var contentLength = response.Content.Headers.ContentLength;

        var buffer = new byte[81920]; // 80KB buffer (default buffer size used by Microsoft's CopyTo method in Stream)
        long totalBytesRead = 0;
        int bytesRead;
        double elapsedSeconds;
        double bytesPerSecond;
        double elapsedSeconds2;
        double bytesPerSecond2;

        // To avoid slowing down the process we should not be saving changes to the context on every iteration
        int saveInterval = 10;
        int counter = 0;

        stopwatch = Stopwatch.StartNew();
        stopwatch2 = Stopwatch.StartNew();

        // Ignore progress reporting when the ContentLength's header is not available
        if (!contentLength.HasValue)
        {
          while ((bytesRead = await download.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
          {
            // Check if cancellation is requested
            if (cancellationToken.IsCancellationRequested)
            {
              throw new OperationCanceledException(cancellationToken);
            }

            await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesRead += bytesRead;

            // Calculate download speed
            elapsedSeconds = stopwatch.Elapsed.TotalNanoseconds;
            bytesPerSecond = elapsedSeconds > 0 ? bytesRead / elapsedSeconds * 1e9 : 0;
            elapsedSeconds2 = stopwatch2.Elapsed.TotalNanoseconds;
            bytesPerSecond2 = elapsedSeconds2 > 0 ? totalBytesRead / elapsedSeconds2 * 1e9 : 0;
            stopwatch.Restart();

            counter++;

            // Inform the client at regular intervals
            if (counter >= saveInterval)
            {
              await WebSocketHandler.SendMessageAsync($"speed:{queueItem.Id}:{bytesPerSecond:F2}");
              await WebSocketHandler.SendMessageAsync($"speedavg:{queueItem.Id}:{bytesPerSecond2:F2}");
              counter = 0;
            }
          }

          queueItem.ProgressPercentage = 100;
          await WebSocketHandler.SendMessageAsync($"progress:{queueItem.Id}:{queueItem.ProgressPercentage}");
          await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
          while ((bytesRead = await download.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
          {
            // Check if cancellation is requested
            if (cancellationToken.IsCancellationRequested)
            {
              throw new OperationCanceledException(cancellationToken);
            }

            await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesRead += bytesRead;
            queueItem.ProgressPercentage = (int)(100.0 * totalBytesRead / contentLength);

            // Calculate download speed
            elapsedSeconds = stopwatch.Elapsed.TotalNanoseconds;
            bytesPerSecond = elapsedSeconds > 0 ? bytesRead / elapsedSeconds * 1e9 : 0;
            elapsedSeconds2 = stopwatch2.Elapsed.TotalNanoseconds;
            bytesPerSecond2 = elapsedSeconds2 > 0 ? totalBytesRead / elapsedSeconds2 * 1e9 : 0;
            stopwatch.Restart();

            counter++;

            // Save progress to the database and inform the client at regular intervals
            if (counter >= saveInterval)
            {
              await _context.SaveChangesAsync(cancellationToken);
              await WebSocketHandler.SendMessageAsync($"progress:{queueItem.Id}:{queueItem.ProgressPercentage}");
              await WebSocketHandler.SendMessageAsync($"speed:{queueItem.Id}:{bytesPerSecond:F2}");
              await WebSocketHandler.SendMessageAsync($"speedavg:{queueItem.Id}:{bytesPerSecond2:F2}");
              counter = 0;
            }
          }

          // Save any remaining changes
          await WebSocketHandler.SendMessageAsync($"progress:{queueItem.Id}:{queueItem.ProgressPercentage}");
          await _context.SaveChangesAsync(cancellationToken);
        }
      }

      // Fixing file permissions on linux
      if (OperatingSystem.IsLinux()) System.IO.File.SetUnixFileMode(localFilePath,
          UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead |
          UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite |
          UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }
    catch (OperationCanceledException)
    {
      // Rethrow the OperationCanceledException to be caught by the caller
      throw;
    }
    catch
    {
      return false;
    }
    finally
    {
      if (stopwatch.IsRunning) stopwatch.Stop();
    }

    return true;
  }
}
