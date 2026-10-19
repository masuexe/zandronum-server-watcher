namespace ZandronumWatcher.Core.Models;

/// <summary>Parsed launcher response for one server.</summary>
public sealed record ServerInfo
{
    public required string Address { get; init; }
    public required string Host { get; init; }
    public required int Port { get; init; }

    public string Name { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Url { get; init; } = "";
    public string MapName { get; init; } = "";
    public string GameName { get; init; } = "";
    public string Iwad { get; init; } = "";
    public string Version { get; init; } = "";
    public string Country { get; init; } = "";

    public GameMode GameMode { get; init; }
    public string GameModeName { get; init; } = "";

    public int MaxClients { get; init; }
    public int MaxPlayers { get; init; }

    /// <summary>
    /// The count the server reported in its SQF_NUMPLAYERS field. This includes bots and is
    /// authoritative even when the detailed roster is missing.
    /// </summary>
    public int NumPlayers { get; init; }

    public IReadOnlyList<string> Pwads { get; init; } = [];
    public IReadOnlyList<PlayerInfo> Players { get; init; } = [];

    /// <summary>
    /// True when the roster is trustworthy enough to tell humans from bots. An empty server
    /// qualifies (zero players is unambiguous); a populated server without a roster does not.
    /// </summary>
    public bool HasPlayerDetails => Players.Count > 0 || NumPlayers == 0;

    /// <summary>
    /// Number of humans connected, bots excluded.
    /// </summary>
    /// <remarks>
    /// This is the number the app cares about: bot-only servers are common (one survey found a
    /// server advertising 36 players, all of them bots), so counting <see cref="NumPlayers"/>
    /// would produce constant false alarms.
    /// </remarks>
    public int HumanCount => Players.Count > 0
        ? Players.Count(p => !p.IsBot)
        : NumPlayers;

    public int BotCount => Players.Count(p => p.IsBot);

    /// <summary>Every connected client, humans and bots alike.</summary>
    public int PlayerCount => Players.Count > 0 ? Players.Count : NumPlayers;

    /// <summary>True when the server requires a password to join.</summary>
    public bool RequiresPassword { get; init; }

    /// <summary>Round-trip time of the query itself.</summary>
    public TimeSpan Latency { get; init; }

    public DateTimeOffset LastUpdated { get; init; }

    /// <summary>Human occupancy, which is what the overlay and the alerts both track.</summary>
    public string SlotText => MaxClients > 0 ? $"{HumanCount}/{MaxClients}" : HumanCount.ToString();
}
