using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ZandronumWatcher.Core.Models;
using ZandronumWatcher.Core.Protocol;

namespace ZandronumWatcher.Core.Services;

/// <inheritdoc />
public sealed class ZandronumClient : IZandronumClient
{
    private const uint LauncherChallenge = 199;
    private const uint MasterChallenge = 5660028;
    private const short MasterProtocolVersion = 2;
    private const int MaxPacketSize = 1400;

    private readonly TimeSpan _timeout;

    public ZandronumClient(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(2);
    }

    public async Task<ServerInfo> QueryServerAsync(
        ServerEndpoint endpoint,
        CancellationToken cancellationToken = default)
    {
        var query = BuildLauncherQuery();
        var stopwatch = Stopwatch.StartNew();
        var response = await ExchangeAsync(endpoint, query, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        var decoded = Huffman.Decode(response);
        return LauncherResponseParser.Parse(decoded, endpoint, stopwatch.Elapsed);
    }

    public async Task<IReadOnlyList<ServerEndpoint>> QueryMasterAsync(
        ServerEndpoint master,
        CancellationToken cancellationToken = default)
    {
        var query = Huffman.Encode(BuildMasterQuery());
        var results = new List<ServerEndpoint>();

        using var client = new UdpClient(AddressFamily.InterNetwork);
        client.Client.ReceiveTimeout = (int)_timeout.TotalMilliseconds;
        await client.SendAsync(query, new IPEndPoint(await ResolveAsync(master.Host, cancellationToken).ConfigureAwait(false), master.Port), cancellationToken)
            .ConfigureAwait(false);

        // The master answers in several datagrams; the last one carries MSC_ENDSERVERLIST.
        while (true)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            UdpReceiveResult received;
            try
            {
                received = await client.ReceiveAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break; // treat a stalled master as "list finished"
            }

            var decoded = Huffman.Decode(received.Buffer);
            var chunk = MasterResponseParser.Parse(decoded);
            results.AddRange(chunk.Endpoints);
            if (chunk.IsFinal)
            {
                break;
            }
        }

        return results;
    }

    private static byte[] BuildLauncherQuery()
    {
        var flags = ServerQueryFlags.Overlay;
        var payload = new byte[16];
        BitConverter.TryWriteBytes(payload.AsSpan(0, 4), LauncherChallenge);
        BitConverter.TryWriteBytes(payload.AsSpan(4, 4), (uint)flags);
        BitConverter.TryWriteBytes(payload.AsSpan(8, 4), (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        BitConverter.TryWriteBytes(payload.AsSpan(12, 4), (uint)ServerQueryFlags2.None);
        return Huffman.Encode(payload);
    }

    private static byte[] BuildMasterQuery()
    {
        var payload = new byte[6];
        BitConverter.TryWriteBytes(payload.AsSpan(0, 4), MasterChallenge);
        BitConverter.TryWriteBytes(payload.AsSpan(4, 2), MasterProtocolVersion);
        return payload;
    }

    private async Task<byte[]> ExchangeAsync(
        ServerEndpoint endpoint,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        var address = await ResolveAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);
        using var client = new UdpClient(AddressFamily.InterNetwork);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        await client.SendAsync(payload, new IPEndPoint(address, endpoint.Port), cts.Token).ConfigureAwait(false);
        var received = await client.ReceiveAsync(cts.Token).ConfigureAwait(false);
        return received.Buffer;
    }

    /// <summary>
    /// Resolves a host name to IPv4. Names are preferred over hard-coded addresses so that
    /// proxy / fake-IP setups (Mihomo TUN and similar) keep working.
    /// </summary>
    private static async Task<IPAddress> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var literal))
        {
            return literal.AddressFamily == AddressFamily.InterNetwork
                ? literal
                : literal.MapToIPv4();
        }

        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
        return ipv4 ?? addresses.FirstOrDefault()
            ?? throw new SocketException((int)SocketError.HostNotFound);
    }
}
