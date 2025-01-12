namespace SaveHere.Helpers
{
  public static class DirectoryBrowser
  {
    private static string _downloadsPath;

    public static string DownloadsPath
    {
      get
      {
        if (string.IsNullOrEmpty(_downloadsPath))
        {
          InitializeDownloadsDirectory();
        }
        return _downloadsPath;
      }
    }

    public static void InitializeDownloadsDirectory()
    {
      try
      {
        _downloadsPath = Path.Combine(Directory.GetCurrentDirectory(), "downloads");
        if (!Directory.Exists(_downloadsPath))
        {
          Directory.CreateDirectory(_downloadsPath);
        }
      }
      catch (Exception ex)
      {
        throw new DirectoryAccessException("Failed to initialize downloads directory.", ex);
      }
    }

    public static List<FileSystemItem> GetDownloadsContent()
    {
      try
      {
        var dirInfo = new DirectoryInfo(DownloadsPath);
        return GetDirectoryContent(dirInfo);
      }
      catch (Exception ex)
      {
        throw new DirectoryAccessException($"Failed to access downloads directory at {DownloadsPath}", ex);
      }
    }

    public static List<FileSystemItem> GetDirectoryContent(DirectoryInfo dirInfo)
    {
      try
      {
        var result = new List<FileSystemItem>();

        foreach (var fileSystemInfo in dirInfo.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
        {
          FileSystemItem item = fileSystemInfo switch
          {
            FileInfo fi => new FileItem
            {
              Name = fi.Name,
              FullName = fi.FullName,
              Type = "file",
              Length = fi.Length,
              Extension = fi.Extension
            },
            DirectoryInfo di => new DirectoryItem
            {
              Name = di.Name,
              FullName = di.FullName,
              Type = "directory",
              Children = GetDirectoryContent(di)
            },
            _ => new FileSystemItem
            {
              Name = fileSystemInfo.Name,
              FullName = fileSystemInfo.FullName,
              Type = "default"
            }
          };

          result.Add(item);
        }

        return result.OrderBy(r => r.Type).ThenBy(r => r.Name).ToList();
      }
      catch (Exception ex)
      {
        throw new DirectoryAccessException($"Failed to read directory content at {dirInfo.FullName}", ex);
      }
    }

    public static void DeleteFileItem(FileItem item)
    {
      File.Delete(item.FullName);
    }
  }

  public class FileSystemItem
  {
    public string Name { get; set; }
    public string FullName { get; set; }
    public string Type { get; set; }
  }

  public class FileItem : FileSystemItem
  {
    public long Length { get; set; }
    public string Extension { get; set; }
  }

  public class DirectoryItem : FileSystemItem
  {
    public List<FileSystemItem> Children { get; set; }
  }

  public class DirectoryAccessException : Exception
  {
    public DirectoryAccessException(string message, Exception innerException = null)
        : base(message, innerException)
    {
    }
  }

}
