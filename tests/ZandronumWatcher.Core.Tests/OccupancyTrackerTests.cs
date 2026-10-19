using ZandronumWatcher.Core.Alerts;
using ZandronumWatcher.Core.Models;
using Xunit;

namespace ZandronumWatcher.Core.Tests;

public class OccupancyTrackerTests
{
    private static readonly ServerEndpoint Endpoint = new("example.org", 10666);

    private static ServerStatus Online(params (string Name, bool IsBot)[] players) =>
        ServerStatus.Online(Endpoint, new ServerInfo
        {
            Address = Endpoint.ToString(),
            Host = Endpoint.Host,
            Port = Endpoint.Port,
            DisplayName = "Test Server",
            MapName = "MAP01",
            GameModeName = "Survival",
            MaxClients = 16,
            NumPlayers = players.Length,
            Players = players
                .Select(p => new PlayerInfo
                {
                    Name = p.Name,
                    DisplayName = p.Name,
                    IsBot = p.IsBot,
                })
                .ToList(),
        });

    private static ServerStatus Offline() => ServerStatus.Offline(Endpoint, "timed out");

    [Fact]
    public void First_observation_is_only_a_baseline()
    {
        var tracker = new OccupancyTracker();

        Assert.Empty(tracker.Observe(Online(("Alice", false))));
    }

    [Fact]
    public void Empty_to_one_human_raises_became_populated()
    {
        var tracker = new OccupancyTracker();
        tracker.Observe(Online());

        var changes = tracker.Observe(Online(("Alice", false)));

        var change = Assert.Single(changes);
        Assert.Equal(OccupancyChangeKind.BecamePopulated, change.Kind);
        Assert.Equal(0, change.PreviousHumans);
        Assert.Equal(1, change.CurrentHumans);
        Assert.Equal(["Alice"], change.Joined);
    }

    [Fact]
    public void Bots_do_not_count_as_population()
    {
        var tracker = new OccupancyTracker();
        tracker.Observe(Online());

        // A server that only spawns bots must not look populated.
        var changes = tracker.Observe(Online(("Bot1", true), ("Bot2", true)));

        Assert.Empty(changes);
    }

    [Fact]
    public void Bots_appearing_do_not_trigger_alerts_even_when_humans_present()
    {
        var tracker = new OccupancyTracker();
        tracker.Observe(Online(("Alice", false)));

        var changes = tracker.Observe(Online(("Alice", false), ("Bot1", true)));

        Assert.Empty(changes);
    }

    [Fact]
    public void Additional_human_raises_human_joined_with_only_the_new_name()
    {
        var tracker = new OccupancyTracker();
        tracker.Observe(Online(("Alice", false)));

        var change = Assert.Single(tracker.Observe(Online(("Alice", false), ("Bob", false))));

        Assert.Equal(OccupancyChangeKind.HumanJoined, change.Kind);
        Assert.Equal(["Bob"], change.Joined);
        Assert.Equal(1, change.PreviousHumans);
        Assert.Equal(2, change.CurrentHumans);
    }

    [Fact]
    public void Last_human_leaving_raises_became_empty()
    {
        var tracker = new OccupancyTracker(new OccupancyAlertOptions { AlertOnBecameEmpty = true });
        tracker.Observe(Online(("Alice", false)));

        var change = Assert.Single(tracker.Observe(Online()));

        Assert.Equal(OccupancyChangeKind.BecameEmpty, change.Kind);
        Assert.Equal(["Alice"], change.Left);
    }

    [Fact]
    public void Human_leaving_is_silent_by_default()
    {
        var tracker = new OccupancyTracker();
        tracker.Observe(Online(("Alice", false), ("Bob", false)));

        Assert.Empty(tracker.Observe(Online(("Alice", false))));
    }

    [Fact]
    public void Single_timeout_does_not_raise_offline()
    {
        var tracker = new OccupancyTracker(new OccupancyAlertOptions { OfflineThreshold = 3 });
        tracker.Observe(Online(("Alice", false)));

        Assert.Empty(tracker.Observe(Offline()));
        Assert.Empty(tracker.Observe(Offline()));
    }

    [Fact]
    public void Repeated_failures_eventually_raise_offline()
    {
        // Offline alerts are opt-in; a server dropping out is noise for most users.
        var tracker = new OccupancyTracker(new OccupancyAlertOptions
        {
            OfflineThreshold = 3,
            AlertOnWentOffline = true,
        });
        tracker.Observe(Online(("Alice", false)));

        tracker.Observe(Offline());
        tracker.Observe(Offline());
        var change = Assert.Single(tracker.Observe(Offline()));

        Assert.Equal(OccupancyChangeKind.WentOffline, change.Kind);
    }

    [Fact]
    public void Recovering_after_offline_raises_came_online()
    {
        var tracker = new OccupancyTracker(new OccupancyAlertOptions { OfflineThreshold = 2 });
        tracker.Observe(Online(("Alice", false)));
        tracker.Observe(Offline());
        tracker.Observe(Offline());

        var change = Assert.Single(tracker.Observe(Online(("Alice", false))));

        Assert.Equal(OccupancyChangeKind.CameOnline, change.Kind);
    }

    [Fact]
    public void Flapping_server_does_not_spam_offline_alerts()
    {
        var tracker = new OccupancyTracker(new OccupancyAlertOptions { OfflineThreshold = 3 });
        tracker.Observe(Online(("Alice", false)));

        // fail, recover, fail, recover: never reaches the threshold
        Assert.Empty(tracker.Observe(Offline()));
        Assert.Empty(tracker.Observe(Online(("Alice", false))));
        Assert.Empty(tracker.Observe(Offline()));
        Assert.Empty(tracker.Observe(Online(("Alice", false))));
    }

    [Fact]
    public void Offline_to_offline_is_silent()
    {
        var tracker = new OccupancyTracker(new OccupancyAlertOptions { OfflineThreshold = 1 });
        tracker.Observe(Online());
        tracker.Observe(Offline());

        Assert.Empty(tracker.Observe(Offline()));
    }

    [Fact]
    public void Alerts_can_be_disabled_individually()
    {
        var tracker = new OccupancyTracker(new OccupancyAlertOptions
        {
            AlertOnBecamePopulated = false,
            AlertOnHumanJoined = false,
        });
        tracker.Observe(Online());

        Assert.Empty(tracker.Observe(Online(("Alice", false))));
        Assert.Empty(tracker.Observe(Online(("Alice", false), ("Bob", false))));
    }

    [Fact]
    public void Detail_reports_the_transition()
    {
        var tracker = new OccupancyTracker();
        tracker.Observe(Online());

        var change = Assert.Single(tracker.Observe(Online(("Alice", false))));

        Assert.Contains("0 → 1/16", change.Detail);
        Assert.Contains("Alice", change.Detail);
        Assert.Contains("Survival", change.Detail);
    }
}
