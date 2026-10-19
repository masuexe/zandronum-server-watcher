using ZandronumWatcher.Core.Alerts;
using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.Core.Services;

/// <summary>
/// Polls the watched servers and raises alerts when occupancy changes.
/// </summary>
/// <remarks>
/// This is the piece both front ends share: the WPF overlay subscribes for its row updates,
/// and a headless host would subscribe with a console sink. Alerting policy lives entirely in
/// <see cref="OccupancyTracker"/> so the two hosts cannot drift apart.
/// </remarks>
public sealed class OccupancyMonitor : IAsyncDisposable
{
    private readonly ServerWatcher _watcher;
    private readonly OccupancyTracker _tracker;
    private readonly List<IAlertSink> _sinks;

    public OccupancyMonitor(
        IZandronumClient client,
        IEnumerable<ServerEndpoint> endpoints,
        ServerQueryOptions? queryOptions = null,
        OccupancyAlertOptions? alertOptions = null,
        IEnumerable<IAlertSink>? sinks = null)
    {
        _watcher = new ServerWatcher(client, endpoints, queryOptions);
        _tracker = new OccupancyTracker(alertOptions);
        _sinks = sinks?.ToList() ?? [];
        _watcher.Refreshed += OnRefreshed;
    }

    /// <summary>Raised for every server after every refresh, regardless of alerts.</summary>
    public event EventHandler<ServerStatus>? StatusUpdated;

    /// <summary>Raised once per detected transition.</summary>
    public event EventHandler<OccupancyChange>? AlertRaised;

    public IReadOnlyList<ServerEndpoint> Endpoints => _watcher.Endpoints;

    public void AddSink(IAlertSink sink) => _sinks.Add(sink);

    public void Start() => _watcher.Start();

    public Task StopAsync() => _watcher.StopAsync();

    public Task<IReadOnlyList<ServerStatus>> RefreshOnceAsync(CancellationToken cancellationToken = default) =>
        _watcher.RefreshOnceAsync(cancellationToken);

    private void OnRefreshed(object? sender, IReadOnlyList<ServerStatus> statuses)
    {
        foreach (var status in statuses)
        {
            StatusUpdated?.Invoke(this, status);

            IReadOnlyList<OccupancyChange> changes;
            try
            {
                changes = _tracker.Observe(status);
            }
            catch (Exception)
            {
                // A malformed reply must not take down the polling loop.
                continue;
            }

            foreach (var change in changes)
            {
                AlertRaised?.Invoke(this, change);
                foreach (var sink in _sinks)
                {
                    // Sinks are best-effort: a failing notifier should never stop monitoring.
                    _ = DeliverAsync(sink, change);
                }
            }
        }
    }

    private static async Task DeliverAsync(IAlertSink sink, OccupancyChange change)
    {
        try
        {
            await sink.NotifyAsync(change).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Swallowed on purpose; see comment above.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _watcher.Refreshed -= OnRefreshed;
        await _watcher.DisposeAsync().ConfigureAwait(false);
    }
}
