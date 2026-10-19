using System.IO;
using ZandronumWatcher.App.Notifications;
using ZandronumWatcher.App.Services;
using ZandronumWatcher.Core.Alerts;
using ZandronumWatcher.Core.Services;

namespace ZandronumWatcher.App;

/// <summary>
/// Runs the watcher without any UI. Useful on a machine with no desktop session, and it is the
/// path a Linux host would use since it touches no WPF types.
/// </summary>
internal static class HeadlessRunner
{
    public static async Task<int> RunAsync(WatchConfig config)
    {
        var endpoints = config.ParseEndpoints();
        if (endpoints.Count == 0)
        {
            Console.Error.WriteLine("No servers configured in " + WatchConfig.DefaultPath);
            return 1;
        }

        var log = new FileAlertSink();
        Console.WriteLine($"watching {endpoints.Count} server(s), logging to {log.LogPath}");

        var options = new ServerQueryOptions
        {
            PollInterval = TimeSpan.FromSeconds(Math.Max(5, config.PollSeconds)),
        };

        await using var monitor = new OccupancyMonitor(
            new ZandronumClient(options.Timeout),
            endpoints,
            options,
            config.ToAlertOptions(),
            [new ToastAlertSink(), log]);

        monitor.AlertRaised += (_, change) => Console.WriteLine($"{change.Headline} — {change.Detail}");

        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Cancel();
        };

        monitor.Start();
        try
        {
            await Task.Delay(Timeout.Infinite, stop.Token);
        }
        catch (OperationCanceledException)
        {
        }

        await monitor.StopAsync();
        return 0;
    }
}
