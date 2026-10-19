using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.Core.Alerts;

/// <summary>Why an alert fired.</summary>
public enum OccupancyChangeKind
{
    /// <summary>The server went from empty to having at least one human.</summary>
    BecamePopulated,

    /// <summary>The last human left.</summary>
    BecameEmpty,

    /// <summary>The human count rose while the server was already populated.</summary>
    HumanJoined,

    /// <summary>The human count fell but the server is still populated.</summary>
    HumanLeft,

    /// <summary>The server started answering again after being unreachable.</summary>
    CameOnline,

    /// <summary>The server stopped answering.</summary>
    WentOffline,
}

/// <summary>One transition worth telling the user about.</summary>
public sealed record OccupancyChange
{
    public required ServerEndpoint Endpoint { get; init; }
    public required OccupancyChangeKind Kind { get; init; }

    /// <summary>Server name as reported by the server, falling back to the address.</summary>
    public required string ServerName { get; init; }

    public int PreviousHumans { get; init; }
    public int CurrentHumans { get; init; }

    /// <summary>Human names that appeared in this transition.</summary>
    public IReadOnlyList<string> Joined { get; init; } = [];

    /// <summary>Human names that disappeared in this transition.</summary>
    public IReadOnlyList<string> Left { get; init; } = [];

    public string? MapName { get; init; }
    public string? GameModeName { get; init; }
    public int MaxClients { get; init; }

    /// <summary>Short headline suitable for a notification title.</summary>
    public string Headline => Kind switch
    {
        OccupancyChangeKind.BecamePopulated => $"有人加入了 {ServerName}",
        OccupancyChangeKind.CameOnline => $"{ServerName} 上线了",
        OccupancyChangeKind.WentOffline => $"{ServerName} 掉线了",
        OccupancyChangeKind.BecameEmpty => $"{ServerName} 空了",
        OccupancyChangeKind.HumanJoined => $"{ServerName} 有人加入",
        OccupancyChangeKind.HumanLeft => $"{ServerName} 有人离开",
        _ => ServerName,
    };

    /// <summary>Detail line describing the transition, e.g. <c>0 → 1 / 16</c>.</summary>
    public string Detail
    {
        get
        {
            var slots = MaxClients > 0 ? $"{CurrentHumans}/{MaxClients}" : CurrentHumans.ToString();
            var parts = new List<string> { $"{PreviousHumans} → {slots}" };

            if (Joined.Count > 0)
            {
                parts.Add(string.Join(", ", Joined));
            }
            else if (Left.Count > 0)
            {
                parts.Add(string.Join(", ", Left));
            }

            var where = string.Join(" · ", new[] { GameModeName, MapName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (!string.IsNullOrWhiteSpace(where))
            {
                parts.Add(where);
            }

            return string.Join("  ·  ", parts);
        }
    }
}
