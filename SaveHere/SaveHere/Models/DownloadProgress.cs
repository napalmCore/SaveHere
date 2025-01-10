namespace SaveHere.Models
{
  public class DownloadProgress
  {
    public required int ItemId { get; init; }
    public required int ProgressPercentage { get; init; }
    public required double CurrentSpeed { get; init; }
    public required double AverageSpeed { get; init; }
  }
}
