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

    public IReadOnlyList<string> Pwads { get; init; } = [];
    public IReadOnlyList<PlayerInfo> Players { get; init; } = [];

    /// <summary>Number of humans connected (bots excluded).</summary>
    public int HumanCount => Players.Count(p => !p.IsBot);

    public int BotCount => Players.Count(p => p.IsBot);

    public int PlayerCount => Players.Count;

    /// <summary>True when the server requires a password to join.</summary>
    public bool RequiresPassword { get; init; }

    /// <summary>Round-trip time of the query itself.</summary>
    public TimeSpan Latency { get; init; }

    public DateTimeOffset LastUpdated { get; init; }

    public string SlotText => MaxClients > 0 ? $"{PlayerCount}/{MaxClients}" : PlayerCount.ToString();
}
