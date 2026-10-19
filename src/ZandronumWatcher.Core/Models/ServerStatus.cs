namespace ZandronumWatcher.Core.Models;

/// <summary>Result of one refresh attempt for a watched server.</summary>
public sealed record ServerStatus
{
    public required ServerEndpoint Endpoint { get; init; }

    public ServerInfo? Info { get; init; }

    /// <summary>Set when the server could not be reached or replied with an error.</summary>
    public string? Error { get; init; }

    /// <summary>Set when the server deliberately ignored the query (rate limiting).</summary>
    public bool WasIgnored { get; init; }

    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.Now;

    public bool IsOnline => Info is not null;

    public static ServerStatus Online(ServerEndpoint endpoint, ServerInfo info) =>
        new() { Endpoint = endpoint, Info = info };

    public static ServerStatus Offline(ServerEndpoint endpoint, string error, bool ignored = false) =>
        new() { Endpoint = endpoint, Error = error, WasIgnored = ignored };
}
