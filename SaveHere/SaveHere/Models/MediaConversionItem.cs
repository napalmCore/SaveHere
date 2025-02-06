using System.ComponentModel.DataAnnotations;

namespace SaveHere.Models
{
  public class MediaConversionItem
  {
    [Key]
    public int Id { get; set; }
    [Required]
    public string InputFile { get; set; } = string.Empty;
    [Required]
    public string OutputFormat { get; set; } = string.Empty;
    public string? CustomOptions { get; set; }
    public EQueueItemStatus Status { get; set; } = EQueueItemStatus.Paused;
    public List<string> OutputLog { get; set; } = new();
    public string PersistedLog { get; set; } = string.Empty;
  }

}
