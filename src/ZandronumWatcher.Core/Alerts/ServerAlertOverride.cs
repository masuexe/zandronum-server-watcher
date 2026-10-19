using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.Core.Alerts;

/// <summary>
/// Per-server alert overrides. A <c>null</c> value means "inherit the global setting".
/// </summary>
/// <remarks>
/// People pin servers for different reasons. One server may be a weekly game night where any
/// join matters; another may be a busy public server where only the first player is interesting.
/// A single global switch forces the noisiest server to dictate the setting for all of them.
/// </remarks>
public sealed class ServerAlertOverride
{
    public bool? AlertOnBecamePopulated { get; set; }

    public bool? AlertOnHumanJoined { get; set; }

    public bool? AlertOnBecameEmpty { get; set; }

    public bool? AlertOnHumanLeft { get; set; }

    public bool? AlertOnWentOffline { get; set; }

    public bool? AlertOnCameOnline { get; set; }

    /// <summary>True when every field is unset, i.e. the override is a no-op.</summary>
    public bool IsEmpty =>
        AlertOnBecamePopulated is null
        && AlertOnHumanJoined is null
        && AlertOnBecameEmpty is null
        && AlertOnHumanLeft is null
        && AlertOnWentOffline is null
        && AlertOnCameOnline is null;
}
