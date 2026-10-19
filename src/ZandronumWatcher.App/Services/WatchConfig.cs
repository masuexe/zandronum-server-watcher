using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZandronumWatcher.Core.Alerts;
using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.App.Services;

/// <summary>User-editable list of servers to watch, stored next to the executable.</summary>
public sealed class WatchConfig
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    [JsonPropertyName("pollSeconds")]
    public int PollSeconds { get; set; } = 15;

    [JsonPropertyName("servers")]
    public List<string> Servers { get; set; } = [];

    [JsonPropertyName("notifications")]
    public NotificationConfig Notifications { get; set; } = new();

    public static string DefaultPath =>
        Path.Combine(AppContext.BaseDirectory, "servers.json");

    public static WatchConfig Load(string? path = null)
    {
        path ??= DefaultPath;

        if (!File.Exists(path))
        {
            var fallback = new WatchConfig();
            fallback.Save(path);
            return fallback;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<WatchConfig>(json, SerializerOptions) ?? new WatchConfig();
        }
        catch (JsonException)
        {
            return new WatchConfig();
        }
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));
        }
        catch (IOException)
        {
            // A read-only install directory should not take the app down.
        }
    }

    public IReadOnlyList<ServerEndpoint> ParseEndpoints() =>
        Servers
            .Select(s => ServerEndpoint.TryParse(s, out var endpoint) ? endpoint : (ServerEndpoint?)null)
            .Where(e => e.HasValue)
            .Select(e => e!.Value)
            .Distinct()
            .ToList();

    public OccupancyAlertOptions ToAlertOptions() => new()
    {
        AlertOnBecamePopulated = Notifications.OnServerBecomesPopulated,
        AlertOnHumanJoined = Notifications.OnHumanJoins,
        AlertOnBecameEmpty = Notifications.OnServerBecomesEmpty,
        AlertOnHumanLeft = false,
        AlertOnWentOffline = Notifications.OnServerGoesOffline,
        AlertOnCameOnline = Notifications.OnServerComesOnline,
    };
}

/// <summary>Which transitions should raise a desktop notification.</summary>
public sealed class NotificationConfig
{
    /// <summary>Notify when a server goes from empty to having at least one human.</summary>
    [JsonPropertyName("onServerBecomesPopulated")]
    public bool OnServerBecomesPopulated { get; set; } = true;

    /// <summary>Notify when another human joins an already-populated server.</summary>
    [JsonPropertyName("onHumanJoins")]
    public bool OnHumanJoins { get; set; } = true;

    /// <summary>Notify when the last human leaves.</summary>
    [JsonPropertyName("onServerBecomesEmpty")]
    public bool OnServerBecomesEmpty { get; set; }

    [JsonPropertyName("onServerGoesOffline")]
    public bool OnServerGoesOffline { get; set; }

    [JsonPropertyName("onServerComesOnline")]
    public bool OnServerComesOnline { get; set; }
}
