using ZandronumWatcher.Core.Alerts;
using ZandronumWatcher.Core.Services;
using Xunit;

namespace ZandronumWatcher.Core.Tests;

/// <summary>
/// End-to-end: real UDP queries against a fake Zandronum server, through the real client,
/// watcher, tracker and monitor. This is the test that would have caught a broken Huffman
/// path or a field-order mistake in the launcher parser.
/// </summary>
public class OccupancyMonitorIntegrationTests
{
    [Fact]
    public async Task Alerts_when_a_human_joins_an_empty_server()
    {
        await using var server = new FakeZandronumServer();
        var alerts = new RecordingSink();

        var options = new ServerQueryOptions { PollInterval = TimeSpan.FromMilliseconds(200) };
        await using var monitor = new OccupancyMonitor(
            new ZandronumClient(TimeSpan.FromSeconds(2)),
            [server.Endpoint],
            options,
            new OccupancyAlertOptions(),
            [alerts]);

        // Baseline: empty server.
        server.SetPlayers();
        await monitor.RefreshOnceAsync();
        Assert.Empty(alerts.Changes);

        // Someone joins.
        server.SetPlayers(("Alice", false));
        await monitor.RefreshOnceAsync();

        var change = Assert.Single(alerts.Changes);
        Assert.Equal(OccupancyChangeKind.BecamePopulated, change.Kind);
        Assert.Equal(0, change.PreviousHumans);
        Assert.Equal(1, change.CurrentHumans);
        Assert.Contains("Alice", change.Joined);
        Assert.Equal("Test Server", change.ServerName);
    }

    [Fact]
    public async Task Bot_only_server_does_not_alert()
    {
        await using var server = new FakeZandronumServer();
        var alerts = new RecordingSink();

        await using var monitor = new OccupancyMonitor(
            new ZandronumClient(TimeSpan.FromSeconds(2)),
            [server.Endpoint],
            new ServerQueryOptions { PollInterval = TimeSpan.FromMilliseconds(200) },
            new OccupancyAlertOptions(),
            [alerts]);

        server.SetPlayers();
        await monitor.RefreshOnceAsync();

        server.SetPlayers(("Bot A", true), ("Bot B", true), ("Bot C", true));
        await monitor.RefreshOnceAsync();

        Assert.Empty(alerts.Changes);
    }

    [Fact]
    public async Task Second_human_raises_human_joined()
    {
        await using var server = new FakeZandronumServer();
        var alerts = new RecordingSink();

        await using var monitor = new OccupancyMonitor(
            new ZandronumClient(TimeSpan.FromSeconds(2)),
            [server.Endpoint],
            new ServerQueryOptions { PollInterval = TimeSpan.FromMilliseconds(200) },
            new OccupancyAlertOptions(),
            [alerts]);

        server.SetPlayers(("Alice", false));
        await monitor.RefreshOnceAsync();

        server.SetPlayers(("Alice", false), ("Bob", false));
        await monitor.RefreshOnceAsync();

        var change = Assert.Single(alerts.Changes);
        Assert.Equal(OccupancyChangeKind.HumanJoined, change.Kind);
        Assert.Equal(["Bob"], change.Joined);
    }

    [Fact]
    public async Task Unreachable_server_reports_offline_without_throwing()
    {
        var alerts = new RecordingSink();
        // Port 1 on loopback is reliably closed; the query should time out cleanly.
        var endpoint = new Core.Models.ServerEndpoint("127.0.0.1", 1);

        await using var monitor = new OccupancyMonitor(
            new ZandronumClient(TimeSpan.FromMilliseconds(300)),
            [endpoint],
            new ServerQueryOptions { PollInterval = TimeSpan.FromMilliseconds(200) },
            new OccupancyAlertOptions(),
            [alerts]);

        var statuses = await monitor.RefreshOnceAsync();

        var status = Assert.Single(statuses);
        Assert.False(status.IsOnline);
        Assert.Empty(alerts.Changes);
    }

    private sealed class RecordingSink : IAlertSink
    {
        private readonly List<OccupancyChange> _changes = [];
        private readonly Lock _gate = new();

        public IReadOnlyList<OccupancyChange> Changes
        {
            get
            {
                lock (_gate)
                {
                    return [.. _changes];
                }
            }
        }

        public Task NotifyAsync(OccupancyChange change, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _changes.Add(change);
            }

            return Task.CompletedTask;
        }
    }
}
