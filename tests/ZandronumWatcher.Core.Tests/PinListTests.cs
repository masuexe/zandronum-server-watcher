using ZandronumWatcher.Core.Configuration;
using ZandronumWatcher.Core.Models;
using Xunit;

namespace ZandronumWatcher.Core.Tests;

public class PinListTests
{
    private static readonly ServerEndpoint A = new("a.example", 10666);
    private static readonly ServerEndpoint B = new("b.example", 10667);

    [Fact]
    public void Pin_adds_once()
    {
        var list = new List<ServerEntry>();

        Assert.True(PinList.Pin(list, A));
        Assert.False(PinList.Pin(list, A));
        Assert.Single(list);
    }

    [Fact]
    public void Unpin_removes_the_matching_entry()
    {
        var list = new List<ServerEntry>();
        PinList.Pin(list, A);
        PinList.Pin(list, B);

        Assert.True(PinList.Unpin(list, A));
        Assert.Single(list);
        Assert.False(PinList.Contains(list, A));
        Assert.True(PinList.Contains(list, B));
    }

    [Fact]
    public void Unpin_of_absent_entry_reports_false()
    {
        var list = new List<ServerEntry>();
        PinList.Pin(list, B);

        Assert.False(PinList.Unpin(list, A));
    }

    [Fact]
    public void Unpin_keeps_overrides_on_other_servers()
    {
        var list = new List<ServerEntry>
        {
            new() { Address = "a.example:10666" },
            new()
            {
                Address = "b.example:10667",
                Alerts = new ServerAlerts { OnHumanJoins = false },
            },
        };

        PinList.Unpin(list, A);

        var remaining = Assert.Single(list);
        Assert.Equal("b.example:10667", remaining.Address);
        Assert.False(remaining.Alerts!.OnHumanJoins);
    }

    /// <summary>
    /// Regression: the dialog previously computed the new state as <c>IsPinned is false</c>,
    /// which is always false for a pinned row — so clicking ✓ never unpinned anything.
    /// </summary>
    [Fact]
    public void Toggle_target_is_the_opposite_of_the_current_state()
    {
        Assert.True(PinList.ToggleTarget(false));
        Assert.False(PinList.ToggleTarget(true));
    }

    [Fact]
    public void Toggle_target_applied_twice_round_trips()
    {
        var list = new List<ServerEntry>();
        var pinned = false;

        for (var i = 0; i < 2; i++)
        {
            var target = PinList.ToggleTarget(pinned);
            if (target)
            {
                PinList.Pin(list, A);
            }
            else
            {
                PinList.Unpin(list, A);
            }

            pinned = target;
        }

        Assert.Empty(list);
        Assert.False(pinned);
    }
}
