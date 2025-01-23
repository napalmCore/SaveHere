using System.Text.Json.Serialization;

namespace SaveHere.Models
{
  public class GitHubRelease
  {
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();
  }
}