using System.Net;
using System.Net.Sockets;
using ZandronumWatcher.Core.Models;
using ZandronumWatcher.Core.Protocol;

namespace ZandronumWatcher.Core.Tests;

/// <summary>
/// A real UDP listener that speaks the Zandronum launcher protocol, used to exercise the
/// client end to end without depending on a live internet server.
/// </summary>
internal sealed class FakeZandronumServer : IAsyncDisposable
{
    private readonly UdpClient _udp;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private volatile Response _response;

    public FakeZandronumServer()
    {
        _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        Port = ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;
        _response = new Response("Test Server", "MAP01", []);
        _loop = Task.Run(() => ListenAsync(_cts.Token));
    }

    public int Port { get; }

    public ServerEndpoint Endpoint => new("127.0.0.1", Port);

    /// <summary>Replaces what the next query returns.</summary>
    public void SetPlayers(params (string Name, bool IsBot)[] players) =>
        _response = _response with
        {
            Players = players.Select(p => new PlayerRecord(p.Name, p.IsBot)).ToList(),
        };

    public void SetName(string name) => _response = _response with { Name = name };

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var received = await _udp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                var reply = Huffman.Encode(BuildResponse(_response));
                await _udp.SendAsync(reply, received.RemoteEndPoint, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                // Ignore; the next iteration retries.
            }
        }
    }

    private static byte[] BuildResponse(Response response)
    {
        var flags = ServerQueryFlags.Name
                    | ServerQueryFlags.MapName
                    | ServerQueryFlags.MaxClients
                    | ServerQueryFlags.GameType
                    | ServerQueryFlags.NumPlayers
                    | ServerQueryFlags.PlayerData;

        var buffer = new List<byte>();
        AddUInt32(buffer, LauncherResponseParser.ChallengeAccepted);
        AddUInt32(buffer, 0);                       // server time
        AddString(buffer, "3.2");                   // version
        AddUInt32(buffer, (uint)flags);

        AddString(buffer, response.Name);
        AddString(buffer, response.MapName);
        buffer.Add(16);                             // max clients
        buffer.Add((byte)GameMode.Cooperative);
        buffer.Add(0);                              // instagib
        buffer.Add(0);                              // buckshot
        buffer.Add((byte)response.Players.Count);   // num players

        foreach (var player in response.Players)
        {
            AddString(buffer, player.Name);
            AddInt16(buffer, 0);                    // frags
            AddInt16(buffer, 30);                   // ping
            buffer.Add(0);                          // spectator
            buffer.Add((byte)(player.IsBot ? 1 : 0));
            buffer.Add(1);                          // minutes on server
        }

        return [.. buffer];
    }

    private static void AddUInt32(List<byte> buffer, uint value) =>
        buffer.AddRange(BitConverter.GetBytes(value));

    private static void AddInt16(List<byte> buffer, short value) =>
        buffer.AddRange(BitConverter.GetBytes(value));

    private static void AddString(List<byte> buffer, string value)
    {
        buffer.AddRange(System.Text.Encoding.UTF8.GetBytes(value));
        buffer.Add(0);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _udp.Dispose();
        _cts.Dispose();
    }

    private sealed record Response(string Name, string MapName, IReadOnlyList<PlayerRecord> Players);

    private sealed record PlayerRecord(string Name, bool IsBot);
}
