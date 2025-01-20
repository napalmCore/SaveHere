namespace SaveHere.Models
{
  public class YoutubeDownloadItem
  {
    public string Url { get; set; } = "";
    public bool IsDownloading { get; set; }
    public bool IsCompleted { get; set; }
    public List<string> OutputLog { get; set; } = new();
    public CancellationTokenSource? CancellationTokenSource { get; set; }
    public string Proxy { get; set; } = "";
    public string Quality { get; set; } = "";
  }
}
