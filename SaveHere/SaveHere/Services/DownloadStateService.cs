using System.Collections.Concurrent;

namespace SaveHere.Services
{
  public class DownloadStateService
  {
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _cancellationTokenSources = new();

    public CancellationTokenSource GetOrAddTokenSource(int downloadId)
    {
      return _cancellationTokenSources.GetOrAdd(downloadId, _ => new CancellationTokenSource());
    }

    public bool TryGetTokenSource(int downloadId, out CancellationTokenSource? tokenSource)
    {
      return _cancellationTokenSources.TryGetValue(downloadId, out tokenSource);
    }

    public void RemoveTokenSource(int downloadId)
    {
      _cancellationTokenSources.TryRemove(downloadId, out _);
    }
  }
}
