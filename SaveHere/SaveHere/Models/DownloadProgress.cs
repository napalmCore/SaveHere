namespace SaveHere.Models
{
  public class DownloadProgress
  {
    public int ItemId { get; init; }
    public int ProgressPercentage { get; init; }
    public double CurrentSpeed { get; init; }
    public double AverageSpeed { get; init; }
  }
}
