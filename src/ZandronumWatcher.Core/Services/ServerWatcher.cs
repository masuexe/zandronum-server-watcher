using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.Core.Services;

/// <summary>
/// Polls a fixed set of servers on an interval and raises an event per completed refresh.
/// </summary>
public sealed class ServerWatcher : IAsyncDisposable
{
    private readonly IZandronumClient _client;
    private readonly ServerQueryOptions _options;
    private readonly List<ServerEndpoint> _endpoints;
    private readonly SemaphoreSlim _concurrency;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public ServerWatcher(
        IZandronumClient client,
        IEnumerable<ServerEndpoint> endpoints,
        ServerQueryOptions? options = null)
    {
        _client = client;
        _endpoints = [.. endpoints];
        _options = options ?? new ServerQueryOptions();
        _concurrency = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrency));
    }

    /// <summary>Raised for each server after every refresh attempt.</summary>
    public event EventHandler<ServerStatus>? StatusUpdated;

    /// <summary>Raised once when a full refresh cycle completes.</summary>
    public event EventHandler<IReadOnlyList<ServerStatus>>? Refreshed;

    public IReadOnlyList<ServerEndpoint> Endpoints => _endpoints;

    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        }

        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    /// <summary>Runs a single refresh cycle immediately.</summary>
    public async Task<IReadOnlyList<ServerStatus>> RefreshOnceAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _endpoints.Select(e => QueryOneAsync(e, cancellationToken)).ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var status in results)
        {
            StatusUpdated?.Invoke(this, status);
        }

        Refreshed?.Invoke(this, results);
        return results;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_options.PollInterval);
        try
        {
            do
            {
                await RefreshOnceAsync(cancellationToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
    }

    private async Task<ServerStatus> QueryOneAsync(ServerEndpoint endpoint, CancellationToken cancellationToken)
    {
        await _concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var info = await _client.QueryServerAsync(endpoint, cancellationToken).ConfigureAwait(false);
            return ServerStatus.Online(endpoint, info);
        }
        catch (Protocol.LauncherException ex)
        {
            return ServerStatus.Offline(endpoint, ex.Message, ex.IsRateLimited);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return ServerStatus.Offline(endpoint, "no reply", timedOut: true);
        }
        catch (Exception ex)
        {
            return ServerStatus.Offline(endpoint, ex.Message);
        }
        finally
        {
            _concurrency.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _concurrency.Dispose();
    }
}