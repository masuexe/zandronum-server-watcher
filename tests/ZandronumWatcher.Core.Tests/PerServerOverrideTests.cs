using ZandronumWatcher.Core.Alerts;
using ZandronumWatcher.Core.Models;
using Xunit;

namespace ZandronumWatcher.Core.Tests;

/// <summary>
/// Pinned servers are not interchangeable: a weekly game night and a busy public server want
/// different alert thresholds. These tests pin that behaviour down.
/// </summary>
public class PerServerOverrideTests
{
    private static readonly ServerEndpoint Quiet = new("quiet.example.org", 10666);
    private static readonly ServerEndpoint Busy = new("busy.example.org", 10666);

    private static ServerStatus Online(ServerEndpoint endpoint, params (string Name, bool IsBot)[] players) =>
        ServerStatus.Online(endpoint, new ServerInfo
        {
            Address = endpoint.ToString(),
            Host = endpoint.Host,
            Port = endpoint.Port,
            DisplayName = endpoint.Host,
            MaxClients = 16,
            NumPlayers = players.Length,
            Players = players
                .Select(p => new PlayerInfo { Name = p.Name, DisplayName = p.Name, IsBot = p.IsBot })
                .ToList(),
        });

    private static ServerStatus Empty(ServerEndpoint endpoint) => Online(endpoint);

    [Fact]
    public void Server_override_can_silence_human_joins_while_global_stays_on()
    {
        var options = new OccupancyAlertOptions { AlertOnHumanJoined = true };
        options.OverrideFor(Busy, new ServerAlertOverride { AlertOnHumanJoined = false });

        var tracker = new OccupancyTracker(options);
        tracker.Observe(Online(Quiet, ("A", false)));
        tracker.Observe(Online(Busy, ("A", false)));

        // Quiet server: global setting applies, so the second human alerts.
        var quietChange = Assert.Single(tracker.Observe(Online(Quiet, ("A", false), ("B", false))));
        Assert.Equal(OccupancyChangeKind.HumanJoined, quietChange.Kind);

        // Busy server: overridden off.
        Assert.Empty(tracker.Observe(Online(Busy, ("A", false), ("B", false))));
    }

    [Fact]
    public void Server_override_can_enable_alerts_the_global_config_disables()
    {
        var options = new OccupancyAlertOptions { AlertOnHumanJoined = false };
        options.OverrideFor(Quiet, new ServerAlertOverride { AlertOnHumanJoined = true });

        var tracker = new OccupancyTracker(options);
        tracker.Observe(Online(Quiet, ("A", false)));
        tracker.Observe(Online(Busy, ("A", false)));

        Assert.Single(tracker.Observe(Online(Quiet, ("A", false), ("B", false))));
        Assert.Empty(tracker.Observe(Online(Busy, ("A", false), ("B", false))));
    }

    [Fact]
    public void Unset_override_fields_inherit_the_global_setting()
    {
        var options = new OccupancyAlertOptions
        {
            AlertOnBecamePopulated = true,
            AlertOnHumanJoined = false,
        };
        // Only override joins; became-populated must still follow the global true.
        options.OverrideFor(Quiet, new ServerAlertOverride { AlertOnHumanJoined = true });

        var tracker = new OccupancyTracker(options);
        tracker.Observe(Empty(Quiet));

        var change = Assert.Single(tracker.Observe(Online(Quiet, ("A", false))));
        Assert.Equal(OccupancyChangeKind.BecamePopulated, change.Kind);
    }

    [Fact]
    public void Empty_override_is_a_no_op()
    {
        var options = new OccupancyAlertOptions { AlertOnHumanJoined = true };
        options.OverrideFor(Quiet, new ServerAlertOverride());

        var tracker = new OccupancyTracker(options);
        tracker.Observe(Online(Quiet, ("A", false)));

        Assert.Single(tracker.Observe(Online(Quiet, ("A", false), ("B", false))));
    }

    [Fact]
    public void Offline_alert_can_be_enabled_for_one_server_only()
    {
        var options = new OccupancyAlertOptions { AlertOnWentOffline = false, OfflineThreshold = 2 };
        options.OverrideFor(Quiet, new ServerAlertOverride { AlertOnWentOffline = true });

        var tracker = new OccupancyTracker(options);
        tracker.Observe(Online(Quiet, ("A", false)));
        tracker.Observe(Online(Busy, ("A", false)));

        tracker.Observe(ServerStatus.Offline(Quiet, "timeout"));
        var quietChange = Assert.Single(tracker.Observe(ServerStatus.Offline(Quiet, "timeout")));
        Assert.Equal(OccupancyChangeKind.WentOffline, quietChange.Kind);

        tracker.Observe(ServerStatus.Offline(Busy, "timeout"));
        Assert.Empty(tracker.Observe(ServerStatus.Offline(Busy, "timeout")));
    }

    [Fact]
    public void IsEnabled_falls_back_to_global_when_no_override_exists()
    {
        var options = new OccupancyAlertOptions { AlertOnBecameEmpty = true };

        Assert.True(options.IsEnabled(Quiet, OccupancyChangeKind.BecameEmpty));
        Assert.False(options.IsEnabled(Quiet, OccupancyChangeKind.HumanLeft));
    }
}
