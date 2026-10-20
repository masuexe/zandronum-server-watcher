using System.Windows;

namespace ZandronumWatcher.App;

/// <summary>Connects the tray icon to the overlay window.</summary>
internal static class TrayBootstrap
{
    public static TrayHost CreateFor(MainWindow window)
    {
        var tray = new TrayHost();

        tray.ToggleRequested += (_, _) => Dispatcher(window).BeginInvoke(() =>
        {
            if (window.IsVisible)
            {
                window.HideToTray();
            }
            else
            {
                window.RestoreFromTray();
            }
        });

        tray.ManageServersRequested += (_, _) => Dispatcher(window).BeginInvoke(() =>
        {
            window.RestoreFromTray();
            window.OpenAddServerDialog();
        });

        tray.AutostartRequested += (_, _) =>
        {
            var enabled = AutostartService.Set(!AutostartService.IsEnabled());
            if (!enabled && AutostartService.IsEnabled())
            {
                MessageBox.Show("Could not change the startup setting.", "Zandronum Watcher",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };

        tray.ExitRequested += (_, _) => Dispatcher(window).BeginInvoke(() =>
        {
            window.AllowClose();
            Application.Current.Shutdown();
        });

        return tray;
    }

    private static System.Windows.Threading.Dispatcher Dispatcher(Window window) =>
        window.Dispatcher;
}