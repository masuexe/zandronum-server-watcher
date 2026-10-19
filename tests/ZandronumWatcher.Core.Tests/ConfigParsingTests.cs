using System.Text.Json;
using ZandronumWatcher.Core.Configuration;
using Xunit;

namespace ZandronumWatcher.Core.Tests;

/// <summary>
/// The config file is meant to be hand-edited, so its parsing has to be forgiving and its
/// shorthand has to keep working. These tests exercise the real WatchConfig type.
/// </summary>
public class ConfigParsingTests
{
    private static WatchConfig Parse(string json) =>
        JsonSerializer.Deserialize<WatchConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        })!;

    [Fact]
    public void Accepts_bare_string_servers()
    {
        var config = Parse("""{ "servers": ["a.example:10666", "b.example:10667"] }""");

        Assert.Equal(2, config.ParseEndpoints().Count);
        Assert.Equal("a.example:10666", config.ParseEndpoints()[0].ToString());
    }

    [Fact]
    public void Accepts_object_servers_with_overrides()
    {
        var config = Parse("""
            {
              "servers": [
                { "address": "busy.example:10666", "alerts": { "onHumanJoins": false } }
              ]
            }
            """);

        var endpoint = Assert.Single(config.ParseEndpoints());
        Assert.Equal("busy.example:10666", endpoint.ToString());

        var options = config.ToAlertOptions();
        Assert.True(options.IsEnabled(endpoint, Core.Alerts.OccupancyChangeKind.BecamePopulated));
        Assert.False(options.IsEnabled(endpoint, Core.Alerts.OccupancyChangeKind.HumanJoined));
    }

    [Fact]
    public void Mixes_strings_and_objects()
    {
        var config = Parse("""
            {
              "servers": [
                "plain.example:10666",
                { "address": "fancy.example:10667", "alerts": { "onServerBecomesEmpty": true } }
              ]
            }
            """);

        Assert.Equal(2, config.ParseEndpoints().Count);
    }

    [Fact]
    public void Unset_override_fields_inherit_global_config()
    {
        var config = Parse("""
            {
              "servers": [ { "address": "x.example:10666", "alerts": { "onHumanJoins": false } } ],
              "notifications": { "onServerBecomesPopulated": true, "onHumanJoins": true }
            }
            """);

        var endpoint = config.ParseEndpoints()[0];
        var options = config.ToAlertOptions();

        Assert.True(options.IsEnabled(endpoint, Core.Alerts.OccupancyChangeKind.BecamePopulated));
        Assert.False(options.IsEnabled(endpoint, Core.Alerts.OccupancyChangeKind.HumanJoined));
    }

    [Fact]
    public void Duplicate_endpoints_are_collapsed()
    {
        var config = Parse("""{ "servers": ["a.example:10666", "a.example:10666"] }""");

        Assert.Single(config.ParseEndpoints());
    }

    [Fact]
    public void Invalid_endpoints_are_skipped()
    {
        var config = Parse("""{ "servers": ["nonsense", "good.example:10666"] }""");

        var endpoint = Assert.Single(config.ParseEndpoints());
        Assert.Equal("good.example:10666", endpoint.ToString());
    }

    [Fact]
    public void Server_without_address_is_ignored()
    {
        var config = Parse("""{ "servers": [ { "alerts": { "onHumanJoins": false } } ] }""");

        Assert.Empty(config.ParseEndpoints());
    }
}
