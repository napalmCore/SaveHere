using System.Text;
using System.Text.Json.Serialization;

namespace SaveHere.Models
{
  public class VideoInfo
  {
    public string Title { get; set; }
    public string Filename { get; set; }
    public string Ext { get; set; }
    public bool RequiresLogin { get; set; }
    public string Errors { get; set; } = string.Empty;

    [JsonPropertyName("filesize_approx")]
    public long? FileSize { get; set; }

    public string ConvertBytesToFileSize(long bytes)
    {
      const long KB = 1024;
      const long MB = KB * 1024;
      const long GB = MB * 1024;
      const long TB = GB * 1024;

      if (bytes >= TB)
        return $"{bytes / (double)TB:F2} TB";
      if (bytes >= GB)
        return $"{bytes / (double)GB:F2} GB";
      if (bytes >= MB)
        return $"{bytes / (double)MB:F2} MB";
      if (bytes >= KB)
        return $"{bytes / (double)KB:F2} KB";

      return $"{bytes} bytes";
    }

    public override string ToString()
    {
      if (Errors == string.Empty)
      {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Title: {Title}<br>");
        sb.AppendLine($"Filename: {Filename}<br>");
        sb.AppendLine($"Extension: {Ext}<br>");
        sb.AppendLine($"Filesize: {(FileSize.HasValue ? ConvertBytesToFileSize(FileSize.Value) : "Unknown")}<br>");
        sb.AppendLine($"Requires Login: {(RequiresLogin ? "Yes" : "No")}<br>");
        return sb.ToString();
      }

      return $"Errors when getting file info: {Errors}<br>";
    }
  }
}
