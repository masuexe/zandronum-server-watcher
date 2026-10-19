using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ZandronumWatcher.App.Notifications;
using ZandronumWatcher.Core.Configuration;
using ZandronumWatcher.Core.Alerts;
using ZandronumWatcher.Core.Models;
using ZandronumWatcher.Core.Services;

namespace ZandronumWatcher.App.ViewModels;

/// <summary>Drives the overlay window: owns the monitor and the visible rows.</summary>
public sealed class OverlayViewModel : ObservableObject, IAsyncDisposable
{
    private readonly WatchConfig _config;
    private OccupancyMonitor? _monitor;
    private string _statusLine = "starting…";
    private bool _isPinned = true;
    private bool _notificationsEnabled = true;

    public OverlayViewModel(WatchConfig config)
    {
        _config = config;
        _notificationsEnabled = config.Notifications.OnServerBecomesPopulated
                                || config.Notifications.OnHumanJoins
                                || config.Notifications.OnServerBecomesEmpty
                                || config.Notifications.OnServerGoesOffline
                                || config.Notifications.OnServerComesOnline;
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

    /// <summary>Whether desktop notifications are currently being delivered.</summary>
    public bool NotificationsEnabled
    {
        get => _notificationsEnabled;
        set
        {
            if (SetProperty(ref _notificationsEnabled, value))
            {
                UpdateStatusLine();
            }
        }
    }

    public string ConfigPath => WatchConfig.DefaultPath;

    /// <summary>Rebuilds rows from the current config and restarts polling.</summary>
    public async void Reload()
    {
        await DisposeMonitorAsync();

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

        _monitor = new OccupancyMonitor(
            new ZandronumClient(options.Timeout),
            endpoints,
            options,
            _config.ToAlertOptions(),
            [new ToastAlertSink()]);

        _monitor.StatusUpdated += OnStatusUpdated;
        _monitor.AlertRaised += OnAlertRaised;
        _monitor.Start();

        UpdateStatusLine();
    }

    private void UpdateStatusLine()
    {
        var endpoints = Rows.Count;
        var interval = Math.Max(5, _config.PollSeconds);
        var notify = _notificationsEnabled ? "notifications on" : "notifications off";
        StatusLine = $"watching {endpoints} server(s) · every {interval}s · {notify}";
    }

    public async Task RefreshNowAsync()
    {
        if (_monitor is not null)
        {
            await _monitor.RefreshOnceAsync();
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

    private void OnAlertRaised(object? sender, OccupancyChange change)
    {
        // The toast sink already delivers the notification; this hook exists so the overlay
        // can flash the row that changed. Kept minimal on purpose.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            return;
        }
    }

    private async Task DisposeMonitorAsync()
    {
        if (_monitor is null)
        {
            return;
        }

        _monitor.StatusUpdated -= OnStatusUpdated;
        _monitor.AlertRaised -= OnAlertRaised;
        await _monitor.DisposeAsync();
        _monitor = null;
    }

    public async ValueTask DisposeAsync() => await DisposeMonitorAsync();
}