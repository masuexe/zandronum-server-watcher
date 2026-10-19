using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.Core.Services;

/// <summary>Network access to Zandronum master servers and individual game servers.</summary>
public interface IZandronumClient
{
    /// <summary>Queries a single server for its launcher information.</summary>
    Task<ServerInfo> QueryServerAsync(ServerEndpoint endpoint, CancellationToken cancellationToken = default);

    /// <summary>Fetches the full server list advertised by a master server.</summary>
    Task<IReadOnlyList<ServerEndpoint>> QueryMasterAsync(
        ServerEndpoint master,
        CancellationToken cancellationToken = default);
}
