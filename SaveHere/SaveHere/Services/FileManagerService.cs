using SaveHere.Helpers;

namespace SaveHere.Services
{
  public interface IFileManagerService
  {
    List<FileSystemItem> GetFiles(string path);
    void DeleteFile(FileItem item);
    void DeleteDirectory(DirectoryItem item);
  }

  public class FileManagerService : IFileManagerService
  {
    public List<FileSystemItem> GetFiles(string path)
    {
      return DirectoryBrowser.GetDirectoryContent(new DirectoryInfo(path)).ToList();
    }

    public void DeleteFile(FileItem item)
    {
      DirectoryBrowser.DeleteFileItem(item);
    }

    public void DeleteDirectory(DirectoryItem item)
    {
      DirectoryBrowser.DeleteDirectoryItem(item);
    }
  }
}
