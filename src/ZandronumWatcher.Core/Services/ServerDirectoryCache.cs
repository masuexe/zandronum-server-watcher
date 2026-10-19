using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.Core.Services;

/// <summary>
/// Caches the result of a master-server scan on disk so the "add server" search is instant.
/// </summary>
/// <remarks>
/// A full scan queries every advertised server and takes tens of seconds. Doing that each time
/// the user opens the add dialog would make search unusable, so the first scan is reused until
/// it goes stale.
/// </remarks>
public sealed class ServerDirectoryCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;

    public ServerDirectoryCache(string path) => _path = path;

    /// <summary>How old a scan may be before it is worth re-running.</summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromMinutes(10);

    public static string DefaultPathFor(string configPath) =>
        Path.ChangeExtension(configPath, ".cache.json");

    /// <summary>Loads the cached scan, or null when there is none or it is unreadable.</summary>
    public CachedScan? Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<CachedScan>(json, SerializerOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public void Save(IReadOnlyList<ServerInfo> servers)
    {
        try
        {
            var scan = new CachedScan
            {
                ScannedAt = DateTimeOffset.Now,
                Servers = [.. servers],
            };

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(scan, SerializerOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A cache that cannot be written is a performance problem, not a failure.
        }
    }

    /// <summary>True when the cached scan exists and is newer than <see cref="MaxAge"/>.</summary>
    public bool IsFresh(CachedScan? scan) =>
        scan is not null && DateTimeOffset.Now - scan.ScannedAt < MaxAge;
}

/// <summary>A stored directory scan.</summary>
public sealed class CachedScan
{
    public DateTimeOffset ScannedAt { get; set; }

    public List<ServerInfo> Servers { get; set; } = [];

    /// <summary>
    /// Case-insensitive match over the fields a player would search by: server name, game
    /// name, map and game mode.
    /// </summary>
    public IEnumerable<ServerInfo> Search(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return Servers;
        }

        return Servers.Where(s =>
            Contains(s.DisplayName, term)
            || Contains(s.GameName, term)
            || Contains(s.MapName, term)
            || Contains(s.GameModeName, term)
            || Contains(s.Iwad, term));
    }

    private static bool Contains(string? haystack, string needle) =>
        !string.IsNullOrEmpty(haystack)
        && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
