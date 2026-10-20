using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.Core.Configuration;

/// <summary>
/// Pure add/remove logic for the pinned list, separated from file IO so it can be tested.
/// </summary>
/// <remarks>
/// The overlay, the add-server dialog and the CLI all edit the same list. Keeping the rules
/// here means a mistake shows up in one test instead of in three UIs that behave differently.
/// </remarks>
public static class PinList
{
    /// <summary>True when <paramref name="list"/> already pins <paramref name="endpoint"/>.</summary>
    public static bool Contains(IEnumerable<ServerEntry> list, ServerEndpoint endpoint) =>
        list.Any(e => ServerEndpoint.TryParse(e.Address, out var parsed) && parsed == endpoint);

    /// <summary>
    /// Adds a pin. Returns false when it was already present, so callers can report
    /// "already pinned" instead of silently duplicating.
    /// </summary>
    public static bool Pin(List<ServerEntry> list, ServerEndpoint endpoint)
    {
        if (Contains(list, endpoint))
        {
            return false;
        }

        list.Add(new ServerEntry { Address = endpoint.ToString() });
        return true;
    }

    /// <summary>Removes a pin, keeping any per-server alert overrides on the others.</summary>
    public static bool Unpin(List<ServerEntry> list, ServerEndpoint endpoint)
    {
        var existing = list.FirstOrDefault(
            e => ServerEndpoint.TryParse(e.Address, out var parsed) && parsed == endpoint);

        return existing is not null && list.Remove(existing);
    }

    /// <summary>The state a toggle should move to: the opposite of the current one.</summary>
    public static bool ToggleTarget(bool currentlyPinned) => !currentlyPinned;
}
