using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZandronumWatcher.Core.Alerts;
using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.Core.Configuration;

/// <summary>User-editable list of pinned servers, stored next to the executable.</summary>
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

    /// <summary>
    /// Pinned servers. Plain strings are accepted for convenience; objects allow per-server
    /// alert overrides.
    /// </summary>
    [JsonPropertyName("servers")]
    public List<ServerEntry> Servers { get; set; } = [];

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
            // A hand-edited config with a typo should not stop the app from starting; the
            // overlay will show an empty list and the user can fix the file.
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

    /// <summary>The pinned endpoints, de-duplicated, in config order.</summary>
    public IReadOnlyList<ServerEndpoint> ParseEndpoints()
    {
        var seen = new HashSet<ServerEndpoint>();
        var result = new List<ServerEndpoint>();

        foreach (var entry in Servers)
        {
            if (ServerEndpoint.TryParse(entry.Address, out var endpoint) && seen.Add(endpoint))
            {
                result.Add(endpoint);
            }
        }

        return result;
    }

    public OccupancyAlertOptions ToAlertOptions()
    {
        var options = new OccupancyAlertOptions
        {
            AlertOnBecamePopulated = Notifications.OnServerBecomesPopulated,
            AlertOnHumanJoined = Notifications.OnHumanJoins,
            AlertOnBecameEmpty = Notifications.OnServerBecomesEmpty,
            AlertOnHumanLeft = Notifications.OnHumanLeaves,
            AlertOnWentOffline = Notifications.OnServerGoesOffline,
            AlertOnCameOnline = Notifications.OnServerComesOnline,
        };

        foreach (var entry in Servers)
        {
            if (!ServerEndpoint.TryParse(entry.Address, out var endpoint) || entry.Alerts is null)
            {
                continue;
            }

            options.OverrideFor(endpoint, entry.Alerts.ToOverride());
        }

        return options;
    }
}

/// <summary>
/// One pinned server. Serialised either as a bare <c>"host:port"</c> string or as an object
/// with per-server overrides.
/// </summary>
[JsonConverter(typeof(ServerEntryConverter))]
public sealed class ServerEntry
{
    public required string Address { get; set; }

    /// <summary>Optional per-server alert overrides; null means "use the global settings".</summary>
    public ServerAlerts? Alerts { get; set; }
}

/// <summary>Per-server notification switches; unset fields inherit the global config.</summary>
public sealed class ServerAlerts
{
    [JsonPropertyName("onServerBecomesPopulated")]
    public bool? OnServerBecomesPopulated { get; set; }

    [JsonPropertyName("onHumanJoins")]
    public bool? OnHumanJoins { get; set; }

    [JsonPropertyName("onServerBecomesEmpty")]
    public bool? OnServerBecomesEmpty { get; set; }

    [JsonPropertyName("onHumanLeaves")]
    public bool? OnHumanLeaves { get; set; }

    [JsonPropertyName("onServerGoesOffline")]
    public bool? OnServerGoesOffline { get; set; }

    [JsonPropertyName("onServerComesOnline")]
    public bool? OnServerComesOnline { get; set; }

    public ServerAlertOverride ToOverride() => new()
    {
        AlertOnBecamePopulated = OnServerBecomesPopulated,
        AlertOnHumanJoined = OnHumanJoins,
        AlertOnBecameEmpty = OnServerBecomesEmpty,
        AlertOnHumanLeft = OnHumanLeaves,
        AlertOnWentOffline = OnServerGoesOffline,
        AlertOnCameOnline = OnServerComesOnline,
    };
}

/// <summary>Reads a server entry from either a string or an object, and always writes an object.</summary>
internal sealed class ServerEntryConverter : JsonConverter<ServerEntry>
{
    public override ServerEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new ServerEntry { Address = reader.GetString() ?? "" };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected a server string or object.");
        }

        string? address = null;
        ServerAlerts? alerts = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException();
            }

            var name = reader.GetString();
            reader.Read();

            if (string.Equals(name, "address", StringComparison.OrdinalIgnoreCase))
            {
                address = reader.GetString();
            }
            else if (string.Equals(name, "alerts", StringComparison.OrdinalIgnoreCase))
            {
                alerts = JsonSerializer.Deserialize<ServerAlerts>(ref reader, options);
            }
            else
            {
                reader.Skip();
            }
        }

        return new ServerEntry { Address = address ?? "", Alerts = alerts };
    }

    public override void Write(Utf8JsonWriter writer, ServerEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("address", value.Address);

        if (value.Alerts is not null)
        {
            writer.WritePropertyName("alerts");
            JsonSerializer.Serialize(writer, value.Alerts, options);
        }

        writer.WriteEndObject();
    }
}

/// <summary>Which transitions should raise a desktop notification.</summary>
public sealed class NotificationConfig
{
    /// <summary>Notify when a pinned server goes from empty to having at least one human.</summary>
    [JsonPropertyName("onServerBecomesPopulated")]
    public bool OnServerBecomesPopulated { get; set; } = true;

    /// <summary>Notify when another human joins an already-populated server.</summary>
    [JsonPropertyName("onHumanJoins")]
    public bool OnHumanJoins { get; set; } = true;

    /// <summary>Notify when the last human leaves.</summary>
    [JsonPropertyName("onServerBecomesEmpty")]
    public bool OnServerBecomesEmpty { get; set; }

    [JsonPropertyName("onHumanLeaves")]
    public bool OnHumanLeaves { get; set; }

    [JsonPropertyName("onServerGoesOffline")]
    public bool OnServerGoesOffline { get; set; }

    [JsonPropertyName("onServerComesOnline")]
    public bool OnServerComesOnline { get; set; }
}