using ZandronumWatcher.Core.Models;
using ZandronumWatcher.Core.Protocol;
using Xunit;

namespace ZandronumWatcher.Core.Tests;

public class ParserTests
{
    private static readonly ServerEndpoint Endpoint = new("example.org", 10666);

    [Fact]
    public void Parses_minimal_launcher_response()
    {
        var writer = new PacketWriter();
        writer.UInt32(LauncherResponseParser.ChallengeAccepted);
        writer.UInt32(0);                       // server time
        writer.String("3.2");                   // version
        writer.UInt32((uint)(ServerQueryFlags.Name | ServerQueryFlags.MapName
                             | ServerQueryFlags.MaxClients | ServerQueryFlags.NumPlayers));
        writer.String("Test Server");
        writer.String("MAP01");
        writer.Byte(16);
        writer.Byte(0);                         // num players

        var info = LauncherResponseParser.Parse(writer.ToArray(), Endpoint, TimeSpan.FromMilliseconds(42));

        Assert.Equal("Test Server", info.DisplayName);
        Assert.Equal("MAP01", info.MapName);
        Assert.Equal(16, info.MaxClients);
        Assert.Empty(info.Players);
        Assert.Equal("3.2", info.Version);
        Assert.Equal(42, (int)info.Latency.TotalMilliseconds);
    }

    [Fact]
    public void Parses_players_and_strips_colour_codes()
    {
        var writer = new PacketWriter();
        writer.UInt32(LauncherResponseParser.ChallengeAccepted);
        writer.UInt32(0);
        writer.String("3.2");
        writer.UInt32((uint)(ServerQueryFlags.Name | ServerQueryFlags.GameType
                             | ServerQueryFlags.MaxClients | ServerQueryFlags.NumPlayers
                             | ServerQueryFlags.PlayerData));
        writer.String("Server");
        writer.Byte((byte)GameMode.Cooperative);
        writer.Byte(0);                         // instagib
        writer.Byte(0);                         // buckshot
        writer.Byte(8);
        writer.Byte(2);                         // num players

        writer.String("\u001c[Red]Alice");
        writer.Int16(10);
        writer.Int16(55);
        writer.Byte(0);                         // not spectator
        writer.Byte(0);                         // not bot
        writer.Byte(3);                         // minutes

        writer.String("Bob");
        writer.Int16(-2);
        writer.Int16(120);
        writer.Byte(0);
        writer.Byte(1);                         // bot
        writer.Byte(9);

        var info = LauncherResponseParser.Parse(writer.ToArray(), Endpoint, TimeSpan.Zero);

        Assert.Equal(2, info.PlayerCount);
        Assert.Equal(1, info.BotCount);
        Assert.Equal("Alice", info.Players[0].DisplayName);
        Assert.Equal(10, info.Players[0].Frags);
        Assert.Equal(-1, info.Players[0].Team);   // non-team mode
        Assert.True(info.Players[1].IsBot);
    }

    [Fact]
    public void Team_game_reads_team_byte()
    {
        var writer = new PacketWriter();
        writer.UInt32(LauncherResponseParser.ChallengeAccepted);
        writer.UInt32(0);
        writer.String("3.2");
        writer.UInt32((uint)(ServerQueryFlags.GameType | ServerQueryFlags.NumPlayers
                             | ServerQueryFlags.PlayerData));
        writer.Byte((byte)GameMode.Ctf);
        writer.Byte(0);
        writer.Byte(0);
        writer.Byte(1);
        writer.String("Carol");
        writer.Int16(1);
        writer.Int16(30);
        writer.Byte(0);
        writer.Byte(0);
        writer.Byte(1);                         // team
        writer.Byte(0);

        var info = LauncherResponseParser.Parse(writer.ToArray(), Endpoint, TimeSpan.Zero);

        Assert.Equal(1, info.Players[0].Team);
    }

    [Fact]
    public void Denied_response_raises_rate_limited_exception()
    {
        var writer = new PacketWriter();
        writer.UInt32(LauncherResponseParser.ChallengeDenied);

        var ex = Assert.Throws<LauncherException>(
            () => LauncherResponseParser.Parse(writer.ToArray(), Endpoint, TimeSpan.Zero));

        Assert.True(ex.IsRateLimited);
    }

    [Fact]
    public void Parses_master_chunk()
    {
        var writer = new PacketWriter();
        writer.UInt32(MasterResponseParser.BeginServerListPart);
        writer.Byte(0);                         // packet number
        writer.Byte(MasterResponseParser.ServerBlock);
        writer.Byte(2);                         // two servers on this IP
        writer.Byte(192); writer.Byte(168); writer.Byte(0); writer.Byte(1);
        writer.UInt16(10666);
        writer.UInt16(10667);
        writer.Byte(0);                         // end of IP list
        writer.Byte(MasterResponseParser.EndServerList);

        var chunk = MasterResponseParser.Parse(writer.ToArray());

        Assert.True(chunk.IsFinal);
        Assert.Equal(2, chunk.Endpoints.Count);
        Assert.Equal("192.168.0.1:10666", chunk.Endpoints[0].ToString());
        Assert.Equal(10667, chunk.Endpoints[1].Port);
    }

    [Fact]
    public void Strip_removes_bracketed_and_plain_escapes()
    {
        Assert.Equal("Red Alert", ZandronumColors.Strip("\u001c[Red]Red \u001cAAlert"));
        Assert.Equal("plain", ZandronumColors.Strip("plain"));
    }
}

/// <summary>Minimal little-endian packet builder for tests.</summary>
internal sealed class PacketWriter
{
    private readonly List<byte> _bytes = [];

    public void Byte(int value) => _bytes.Add((byte)value);

    public void Int16(short value) => _bytes.AddRange(BitConverter.GetBytes(value));

    public void UInt16(ushort value) => _bytes.AddRange(BitConverter.GetBytes(value));

    public void UInt32(uint value) => _bytes.AddRange(BitConverter.GetBytes(value));

    public void String(string value)
    {
        _bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(value));
        _bytes.Add(0);
    }

    public byte[] ToArray() => [.. _bytes];
}
