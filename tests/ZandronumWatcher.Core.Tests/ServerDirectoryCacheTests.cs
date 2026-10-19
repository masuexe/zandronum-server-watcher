using ZandronumWatcher.Core.Models;
using ZandronumWatcher.Core.Services;
using Xunit;

namespace ZandronumWatcher.Core.Tests;

public class ServerDirectoryCacheTests
{
    private static ServerInfo Server(string name, string map = "MAP01", string mode = "Cooperative", int humans = 0) =>
        new()
        {
            Address = "1.2.3.4:10666",
            Host = "1.2.3.4",
            Port = 10666,
            DisplayName = name,
            MapName = map,
            GameModeName = mode,
            MaxClients = 16,
            NumPlayers = humans,
        };

    [Fact]
    public void Round_trips_through_disk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"zsw-cache-{Guid.NewGuid():N}.json");
        try
        {
            var cache = new ServerDirectoryCache(path);
            cache.Save([Server("Test Server", humans: 2)]);

            var loaded = cache.Load();

            Assert.NotNull(loaded);
            var server = Assert.Single(loaded!.Servers);
            Assert.Equal("Test Server", server.DisplayName);
            Assert.Equal(2, server.HumanCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Missing_file_returns_null()
    {
        var cache = new ServerDirectoryCache(
            Path.Combine(Path.GetTempPath(), $"zsw-missing-{Guid.NewGuid():N}.json"));

        Assert.Null(cache.Load());
    }

    [Fact]
    public void Corrupt_file_returns_null_instead_of_throwing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"zsw-corrupt-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ this is not json");
            var cache = new ServerDirectoryCache(path);

            Assert.Null(cache.Load());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Search_matches_name_map_mode_and_iwad()
    {
        var scan = new CachedScan
        {
            Servers =
            [
                Server("Complex Doom Invasion", "CDM27", "Invasion"),
                Server("Vanilla Deathmatch", "MAP01", "Deathmatch"),
            ],
        };

        Assert.Single(scan.Search("complex"));
        Assert.Single(scan.Search("CDM27"));
        Assert.Single(scan.Search("deathmatch"));
        Assert.Empty(scan.Search("nonexistent"));
    }

    [Fact]
    public void Empty_search_returns_everything()
    {
        var scan = new CachedScan { Servers = [Server("A"), Server("B")] };

        Assert.Equal(2, scan.Search("").Count());
        Assert.Equal(2, scan.Search("   ").Count());
    }

    [Fact]
    public void Freshness_respects_max_age()
    {
        var cache = new ServerDirectoryCache("unused.json") { MaxAge = TimeSpan.FromMinutes(10) };

        Assert.True(cache.IsFresh(new CachedScan { ScannedAt = DateTimeOffset.Now.AddMinutes(-1) }));
        Assert.False(cache.IsFresh(new CachedScan { ScannedAt = DateTimeOffset.Now.AddMinutes(-30) }));
        Assert.False(cache.IsFresh(null));
    }

    [Fact]
    public void Cache_path_sits_next_to_the_config()
    {
        var cachePath = ServerDirectoryCache.DefaultPathFor(@"C:\app\servers.json");

        Assert.Equal(@"C:\app\servers.cache.json", cachePath);
    }
}
