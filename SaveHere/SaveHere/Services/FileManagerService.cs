using SaveHere.Helpers;

namespace SaveHere.Services
{
  public interface IFileManagerService
  {
    List<FileItem> GetFiles();
    void DeleteFile(FileItem item);
  }

  public class FileManagerService : IFileManagerService
  {
    public List<FileItem> GetFiles()
    {
      return DirectoryBrowser.GetDownloadsContent()
          .OfType<FileItem>()
          .ToList();
    }

    public void DeleteFile(FileItem item)
    {
      DirectoryBrowser.DeleteFileItem(item);
    }
  }
}
