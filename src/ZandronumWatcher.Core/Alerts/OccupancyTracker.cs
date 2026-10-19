using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.Core.Alerts;

/// <summary>Options controlling which transitions produce alerts.</summary>
public sealed class OccupancyAlertOptions
{
    /// <summary>Fire when a server goes from zero humans to one or more.</summary>
    public bool AlertOnBecamePopulated { get; set; } = true;

    /// <summary>Fire when the last human leaves.</summary>
    public bool AlertOnBecameEmpty { get; set; }

    /// <summary>Fire on every additional human after the server is already populated.</summary>
    public bool AlertOnHumanJoined { get; set; } = true;

    /// <summary>Fire when humans leave but the server stays populated.</summary>
    public bool AlertOnHumanLeft { get; set; }

    /// <summary>Fire when a server stops answering.</summary>
    public bool AlertOnWentOffline { get; set; }

    /// <summary>Fire when a server starts answering again.</summary>
    public bool AlertOnCameOnline { get; set; } = true;

    /// <summary>
    /// How many consecutive failed queries are needed before a server counts as offline.
    /// </summary>
    /// <remarks>
    /// Zandronum servers are UDP and drop queries routinely, so a single timeout is not
    /// evidence that a server is down. Without this debounce a flaky connection produces a
    /// stream of "went offline" / "came online" pairs.
    /// </remarks>
    public int OfflineThreshold { get; set; } = 3;
}

/// <summary>
/// Turns a stream of per-server status snapshots into meaningful transitions.
/// </summary>
/// <remarks>
/// The tracker is deliberately stateful and must see every refresh, including failures. A
/// server that drops off the master list or stops answering must not be treated as "0 players"
/// — that would fire a spurious "server is empty" alert and then another "someone joined" when
/// it comes back.
/// </remarks>
public sealed class OccupancyTracker
{
    private readonly OccupancyAlertOptions _options;
    private readonly Dictionary<ServerEndpoint, Snapshot> _previous = [];
    private readonly Dictionary<ServerEndpoint, int> _consecutiveFailures = [];

    public OccupancyTracker(OccupancyAlertOptions? options = null)
    {
        _options = options ?? new OccupancyAlertOptions();
    }

    /// <summary>Compares a fresh status against the last one and returns any alerts to raise.</summary>
    public IReadOnlyList<OccupancyChange> Observe(ServerStatus status)
    {
        var current = Snapshot.From(status);
        var hasPrevious = _previous.TryGetValue(status.Endpoint, out var previous);

        // Track failures separately from the last good snapshot so that a transient timeout
        // neither clears the roster nor trips the offline alert.
        var failures = 0;
        if (!current.IsOnline)
        {
            failures = _consecutiveFailures.TryGetValue(status.Endpoint, out var n) ? n + 1 : 1;
            _consecutiveFailures[status.Endpoint] = failures;
        }
        else
        {
            _consecutiveFailures[status.Endpoint] = 0;
        }

        var threshold = Math.Max(1, _options.OfflineThreshold);
        var effectiveOffline = !current.IsOnline && failures >= threshold;

        // Keep reporting the last good snapshot until the server is genuinely offline.
        if (!current.IsOnline && !effectiveOffline && hasPrevious)
        {
            return [];
        }

        _previous[status.Endpoint] = current;

        if (!hasPrevious)
        {
            // The first observation is a baseline, not a transition. Firing here would alert
            // about every already-populated server on startup.
            return [];
        }

        var changes = new List<OccupancyChange>();
        var endpoint = status.Endpoint;

        if (previous.IsOnline != current.IsOnline)
        {
            if (!current.IsOnline && _options.AlertOnWentOffline)
            {
                changes.Add(new OccupancyChange
                {
                    Endpoint = endpoint,
                    Kind = OccupancyChangeKind.WentOffline,
                    ServerName = previous.Name ?? endpoint.ToString(),
                    PreviousHumans = previous.Humans,
                    CurrentHumans = 0,
                });
            }
            else if (current.IsOnline && _options.AlertOnCameOnline)
            {
                changes.Add(new OccupancyChange
                {
                    Endpoint = endpoint,
                    Kind = OccupancyChangeKind.CameOnline,
                    ServerName = current.Name ?? endpoint.ToString(),
                    PreviousHumans = 0,
                    CurrentHumans = current.Humans,
                    MapName = current.MapName,
                    GameModeName = current.GameModeName,
                    MaxClients = current.MaxClients,
                });
            }

            return changes;
        }

        if (!current.IsOnline)
        {
            return changes;
        }

        if (previous.Humans == current.Humans)
        {
            return changes;
        }

        var joined = current.HumanNames.Except(previous.HumanNames, StringComparer.Ordinal).ToList();
        var left = previous.HumanNames.Except(current.HumanNames, StringComparer.Ordinal).ToList();

        var kind = (previous.Humans, current.Humans) switch
        {
            (0, > 0) => OccupancyChangeKind.BecamePopulated,
            (> 0, 0) => OccupancyChangeKind.BecameEmpty,
            _ when current.Humans > previous.Humans => OccupancyChangeKind.HumanJoined,
            _ => OccupancyChangeKind.HumanLeft,
        };

        var enabled = kind switch
        {
            OccupancyChangeKind.BecamePopulated => _options.AlertOnBecamePopulated,
            OccupancyChangeKind.BecameEmpty => _options.AlertOnBecameEmpty,
            OccupancyChangeKind.HumanJoined => _options.AlertOnHumanJoined,
            OccupancyChangeKind.HumanLeft => _options.AlertOnHumanLeft,
            _ => false,
        };

        if (enabled)
        {
            changes.Add(new OccupancyChange
            {
                Endpoint = endpoint,
                Kind = kind,
                ServerName = current.Name ?? endpoint.ToString(),
                PreviousHumans = previous.Humans,
                CurrentHumans = current.Humans,
                Joined = joined,
                Left = left,
                MapName = current.MapName,
                GameModeName = current.GameModeName,
                MaxClients = current.MaxClients,
            });
        }

        return changes;
    }

    /// <summary>Forgets all history, e.g. after the watch list changes.</summary>
    public void Reset()
    {
        _previous.Clear();
        _consecutiveFailures.Clear();
    }

    /// <summary>The most recent snapshot for an endpoint, if any.</summary>
    public bool TryGetLast(ServerEndpoint endpoint, out ServerStatus status)
    {
        if (_previous.TryGetValue(endpoint, out var snapshot))
        {
            status = snapshot.Source;
            return true;
        }

        status = null!;
        return false;
    }

    private readonly record struct Snapshot(
        bool IsOnline,
        int Humans,
        string? Name,
        string? MapName,
        string? GameModeName,
        int MaxClients,
        IReadOnlyList<string> HumanNames,
        ServerStatus Source)
    {
        public static Snapshot From(ServerStatus status)
        {
            if (status.Info is not { } info)
            {
                return new Snapshot(false, 0, null, null, null, 0, [], status);
            }

            var humans = info.Players
                .Where(p => !p.IsBot)
                .Select(p => p.DisplayName)
                .ToList();

            return new Snapshot(
                true,
                info.HumanCount,
                info.DisplayName,
                info.MapName,
                info.GameModeName,
                info.MaxClients,
                humans,
                status);
        }
    }
}
