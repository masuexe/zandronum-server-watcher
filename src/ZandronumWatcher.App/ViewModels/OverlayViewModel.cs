using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ZandronumWatcher.App.Services;
using ZandronumWatcher.Core.Models;
using ZandronumWatcher.Core.Services;

namespace ZandronumWatcher.App.ViewModels;

/// <summary>Drives the overlay window: owns the watcher and the visible rows.</summary>
public sealed class OverlayViewModel : ObservableObject, IAsyncDisposable
{
    private readonly WatchConfig _config;
    private ServerWatcher? _watcher;
    private string _statusLine = "starting…";
    private bool _isPinned = true;

    public OverlayViewModel(WatchConfig config)
    {
        _config = config;
        Reload();
    }

    public ObservableCollection<ServerRowViewModel> Rows { get; } = [];

    public string StatusLine
    {
        get => _statusLine;
        private set => SetProperty(ref _statusLine, value);
    }

    public bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }

    public string ConfigPath => WatchConfig.DefaultPath;

    /// <summary>Rebuilds rows from the current config and restarts polling.</summary>
    public async void Reload()
    {
        await DisposeWatcherAsync();

        Rows.Clear();
        var endpoints = _config.ParseEndpoints();
        if (endpoints.Count == 0)
        {
            StatusLine = "no servers configured — edit servers.json";
            return;
        }

        foreach (var endpoint in endpoints)
        {
            Rows.Add(new ServerRowViewModel(endpoint));
        }

        var options = new ServerQueryOptions
        {
            PollInterval = TimeSpan.FromSeconds(Math.Max(5, _config.PollSeconds)),
        };

        _watcher = new ServerWatcher(new ZandronumClient(options.Timeout), endpoints, options);
        _watcher.StatusUpdated += OnStatusUpdated;
        _watcher.Start();

        StatusLine = $"watching {endpoints.Count} server(s) · every {options.PollInterval.TotalSeconds:F0}s";
    }

    public async Task RefreshNowAsync()
    {
        if (_watcher is not null)
        {
            await _watcher.RefreshOnceAsync();
        }
    }

    public void ToggleExpanded(ServerRowViewModel row) => row.IsExpanded = !row.IsExpanded;

    public void OpenConfig()
    {
        var path = WatchConfig.DefaultPath;
        if (!File.Exists(path))
        {
            _config.Save(path);
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
        {
            UseShellExecute = true,
        });
    }

    private void OnStatusUpdated(object? sender, ServerStatus status)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Apply(status);
            return;
        }

        dispatcher.BeginInvoke(() => Apply(status));
    }

    private void Apply(ServerStatus status)
    {
        var row = Rows.FirstOrDefault(r => r.Endpoint == status.Endpoint);
        if (row is not null)
        {
            row.Status = status;
        }
    }

    private async Task DisposeWatcherAsync()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.StatusUpdated -= OnStatusUpdated;
        await _watcher.DisposeAsync();
        _watcher = null;
    }

    public async ValueTask DisposeAsync() => await DisposeWatcherAsync();
}
