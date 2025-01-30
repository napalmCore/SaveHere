namespace SaveHere.Models
{
  public class VersionState
  {
    public bool UpdateAvailable { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public string RepoUrl { get; } = "https://github.com/gudarzi/SaveHere";
  }
}