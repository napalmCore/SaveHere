﻿using System.ComponentModel.DataAnnotations;

namespace SaveHere.Models
{
  public class FileDownloadQueueItem
  {
    [Key]
    public int Id { get; set; }

    [Required]
    [Url]
    public string? InputUrl { get; set; }

    public EQueueItemStatus Status { get; set; } = EQueueItemStatus.Paused;

    public int ProgressPercentage { get; set; } = 0;

    public bool bShowMoreOptions { get; set; } = false;

    public bool bShouldGetFilenameFromHttpHeaders { get; set; } = true;

    public double CurrentDownloadSpeed { get; set; } = 0;
    public double AverageDownloadSpeed { get; set; } = 0;

    public string? DownloadFolder { get; set; }

    //Track the download speed history of a file
    public List<double> SpeedHistory { get; set; } = new List<double>();
  }
}
