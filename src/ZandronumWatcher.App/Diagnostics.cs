using System.IO;

namespace ZandronumWatcher.App;

/// <summary>
/// Appends troubleshooting lines to %LOCALAPPDATA%\ZandronumWatcher\tray.log.
/// </summary>
/// <remarks>
/// The app is a WinExe, so there is no console to inspect. Tray registration failures are
/// otherwise completely silent: the icon can appear in the notification area and still never
/// deliver a click.
/// </remarks>
internal static class Diagnostics
{
    private static readonly Lock Gate = new();

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ZandronumWatcher",
        "tray.log");

    public static void Log(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
            // Diagnostics must never affect behaviour.
        }
    }
}
