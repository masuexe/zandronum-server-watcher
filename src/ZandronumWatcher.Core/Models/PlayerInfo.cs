namespace ZandronumWatcher.Core.Models;

/// <summary>A single player as reported in the SQF_PLAYERDATA block.</summary>
public sealed record PlayerInfo
{
    public required string Name { get; init; }

    /// <summary>Name with Zandronum colour escapes stripped, safe to render.</summary>
    public required string DisplayName { get; init; }

    public short Frags { get; init; }
    public short Ping { get; init; }
    public bool IsSpectator { get; init; }
    public bool IsBot { get; init; }

    /// <summary>Team index, or -1 in non-team game modes.</summary>
    public int Team { get; init; } = -1;

    /// <summary>Minutes the player has been connected.</summary>
    public byte TimeOnServer { get; init; }
}
