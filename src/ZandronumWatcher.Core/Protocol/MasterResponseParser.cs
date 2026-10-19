using System.Net;
using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.Core.Protocol;

/// <summary>Parses one decoded master-server response packet.</summary>
public static class MasterResponseParser
{
    public const uint BeginServerListPart = 6;
    public const uint IpIsBanned = 3;
    public const uint RequestIgnored = 4;
    public const uint WrongVersion = 5;
    public const byte ServerBlock = 8;
    public const byte EndServerList = 2;
    public const byte EndServerListPart = 7;

    public static MasterChunk Parse(ReadOnlySpan<byte> payload)
    {
        var reader = new PacketReader(payload);
        var status = reader.ReadUInt32();
        if (status != BeginServerListPart)
        {
            var reason = status switch
            {
                IpIsBanned => "master server banned this IP",
                RequestIgnored => "master server ignored the request",
                WrongVersion => "master server rejected the protocol version",
                _ => $"unexpected master status {status}",
            };
            throw new LauncherException(reason, status == RequestIgnored);
        }

        _ = reader.ReadByte(); // packet number
        var block = reader.ReadByte();
        if (block != ServerBlock)
        {
            throw new InvalidDataException($"Expected server block {ServerBlock}, got {block}.");
        }

        var endpoints = new List<ServerEndpoint>();
        while (true)
        {
            var count = reader.ReadByte();
            if (count == 0)
            {
                break;
            }

            var a = reader.ReadByte();
            var b = reader.ReadByte();
            var c = reader.ReadByte();
            var d = reader.ReadByte();
            var address = new IPAddress([a, b, c, d]);

            for (var i = 0; i < count; i++)
            {
                var port = reader.ReadUInt16();
                endpoints.Add(new ServerEndpoint(address.ToString(), port));
            }
        }

        var closing = reader.ReadByte();
        var isFinal = closing == EndServerList;
        if (!isFinal && closing != EndServerListPart)
        {
            throw new InvalidDataException($"Unexpected closing status {closing}.");
        }

        return new MasterChunk(endpoints, isFinal);
    }
}

/// <summary>One chunk of a paginated master-server reply.</summary>
public readonly record struct MasterChunk(IReadOnlyList<ServerEndpoint> Endpoints, bool IsFinal);
