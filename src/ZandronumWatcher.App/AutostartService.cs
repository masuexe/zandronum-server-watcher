using System.IO;
using Microsoft.Win32;

namespace ZandronumWatcher.App;

/// <summary>
/// Registers the app to launch at sign-in, so the watcher survives a reboot.
/// </summary>
/// <remarks>
/// The whole point of this tool is to sit in the background and tell you when a server wakes
/// up. Without autostart, every reboot silently turns notifications off until you remember to
/// launch it again — the failure mode is invisible, which is the worst kind.
/// Uses the per-user Run key: no admin rights, and easy to undo.
/// </remarks>
internal static class AutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ZandronumWatcher";

    /// <summary>True when the Run entry exists and points at this executable.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            if (key?.GetValue(ValueName) is not string command)
            {
                return false;
            }

            var expected = Quote(Environment.ProcessPath);
            return string.Equals(command, expected, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>Adds or removes the Run entry. Returns the resulting state.</summary>
    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null)
            {
                return false;
            }

            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe))
                {
                    return false;
                }

                key.SetValue(ValueName, Quote(exe));
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return IsEnabled() == enabled;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>Windows requires the path to be quoted when it contains spaces.</summary>
    private static string Quote(string? path) => $"\"{path}\"";
}
