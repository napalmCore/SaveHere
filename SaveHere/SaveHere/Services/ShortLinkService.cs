using SaveHere.Helpers;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SaveHere.Services
{
  public class ShortLinkService
  {
    private readonly ConcurrentDictionary<string, string> _shortLinksToFilePaths = new();
    private readonly ConcurrentDictionary<string, string> _filePathsToShortLinks = new();
    private readonly object _lock = new();

    public string GetShortLink(string filePath)
    {
      // If we already have a short link for this file, return it
      if (_filePathsToShortLinks.TryGetValue(filePath, out var existingShortLink))
      {
        return existingShortLink;
      }

      // Generate a deterministic short link based on the file path
      string shortLink = GenerateShortLink(filePath);

      // Store the mapping
      _shortLinksToFilePaths[shortLink] = filePath;
      _filePathsToShortLinks[filePath] = shortLink;

      return shortLink;
    }

    public bool TryGetFilePath(string shortLink, out string filePath)
    {
      return _shortLinksToFilePaths.TryGetValue(shortLink, out filePath);
    }

    public void RefreshShortLinks(List<FileSystemItem> files)
    {
      // Create a new dictionary for the updated mappings
      var newFilePathsToShortLinks = new ConcurrentDictionary<string, string>();
      var newShortLinksToFilePaths = new ConcurrentDictionary<string, string>();

      // Process files in parallel for better performance
      Parallel.ForEach(files, file =>
      {
        if (file is FileItem fileItem)
        {
          string filePath = fileItem.FullName;

          // Try to reuse existing short link if available
          if (!_filePathsToShortLinks.TryGetValue(filePath, out var shortLink))
          {
            shortLink = GenerateShortLink(filePath);
          }

          newFilePathsToShortLinks[filePath] = shortLink;
          newShortLinksToFilePaths[shortLink] = filePath;
        }
      });

      // Replace the dictionaries with the new ones
      lock (_lock)
      {
        _filePathsToShortLinks.Clear();
        _shortLinksToFilePaths.Clear();

        foreach (var pair in newFilePathsToShortLinks)
        {
          _filePathsToShortLinks[pair.Key] = pair.Value;
        }

        foreach (var pair in newShortLinksToFilePaths)
        {
          _shortLinksToFilePaths[pair.Key] = pair.Value;
        }
      }
    }

    private string GenerateShortLink(string filePath)
    {
      // Use SHA256 to create a deterministic hash based on the file path
      using (var sha256 = SHA256.Create())
      {
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(filePath));
        return Convert.ToHexString(hashBytes).Substring(0, 16).ToLowerInvariant();
      }
    }
  }
}