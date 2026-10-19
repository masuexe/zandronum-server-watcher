using System.IO;
using ZandronumWatcher.Core.Alerts;

namespace ZandronumWatcher.App.Notifications;

/// <summary>
/// Appends alerts to a log file. Used by headless mode, where there is no window to show and
/// no console attached to the WinExe.
/// </summary>
internal sealed class FileAlertSink : IAlertSink
{
    private readonly string _logPath;
    private readonly Lock _gate = new();

    public FileAlertSink(string? logPath = null)
    {
        _logPath = logPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZandronumWatcher",
            "watcher.log");

        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
    }

    /// <summary>Where alerts are written. Named LogPath to avoid shadowing System.IO.Path.</summary>
    public string LogPath => _logPath;

    public Task NotifyAsync(OccupancyChange change, CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_gate)
            {
                File.AppendAllText(
                    _logPath,
                    $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {change.Kind} | {change.Headline} | {change.Detail}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
            // Logging must never break monitoring.
        }

        return Task.CompletedTask;
    }
}
