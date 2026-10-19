using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.Core.Services;

/// <summary>
/// Queries the public master server and reports which listed servers are actually reachable.
/// </summary>
/// <remarks>
/// This exists to answer "which servers should I pin?". Roughly half of the entries a master
/// server advertises do not respond, so handing the raw list to a user would be mostly noise.
/// </remarks>
public sealed class ServerDirectory
{
    private readonly IZandronumClient _client;
    private readonly ServerQueryOptions _options;

    public ServerDirectory(IZandronumClient client, ServerQueryOptions? options = null)
    {
        _client = client;
        _options = options ?? new ServerQueryOptions();
    }

    /// <summary>Fetches the master list and queries every entry, returning reachable servers.</summary>
    /// <param name="master">Master server to query.</param>
    /// <param name="onProgress">
    /// Invoked with the number of servers queried so far. Deliberately an
    /// <see cref="Action{T}"/> rather than <see cref="IProgress{T}"/>: <c>Progress&lt;T&gt;</c>
    /// captures the ambient SynchronizationContext, which deadlocks when a WPF caller blocks on
    /// the returned task.
    /// </param>
    /// <param name="cancellationToken">Cancels the discovery.</param>
    public async Task<IReadOnlyList<ServerInfo>> DiscoverAsync(
        ServerEndpoint master,
        Action<int>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var endpoints = await _client.QueryMasterAsync(master, cancellationToken).ConfigureAwait(false);
        onProgress?.Invoke(0);

        var results = new List<ServerInfo>();
        var completed = 0;
        var gate = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrency));

        var tasks = endpoints.Select(async endpoint =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var info = await _client.QueryServerAsync(endpoint, cancellationToken).ConfigureAwait(false);
                lock (results)
                {
                    results.Add(info);
                }
            }
            catch (Exception)
            {
                // Unreachable servers are expected; they are simply not offered.
            }
            finally
            {
                gate.Release();
                onProgress?.Invoke(Interlocked.Increment(ref completed));
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        gate.Dispose();

        return [.. results.OrderByDescending(r => r.HumanCount).ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)];
    }
}
