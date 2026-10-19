using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using ZandronumWatcher.App.Services;

namespace ZandronumWatcher.App;

public partial class App : Application
{
    private const string MutexName = @"Local\ZandronumWatcher.SingleInstance";

    private Mutex? _mutex;

    /// <summary>Set when the process runs without a window.</summary>
    public static bool IsHeadless { get; private set; }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    protected override void OnStartup(StartupEventArgs e)
    {
        IsHeadless = e.Args.Any(a => a.Equals("--headless", StringComparison.OrdinalIgnoreCase));

        _mutex = new Mutex(true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            if (!IsHeadless)
            {
                MessageBox.Show("Zandronum Watcher is already running.", "Zandronum Watcher",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Shutdown();
            return;
        }

        // Unpackaged desktop apps need a Start Menu shortcut with an AppUserModelID before
        // Windows will deliver toast notifications.
        Notifications.AppRegistration.Register();

        if (IsHeadless)
        {
            RunHeadless(e.Args);
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);

        // StartupUri is intentionally absent: headless mode must not instantiate a window.
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void RunHeadless(string[] args)
    {
        // A WinExe has no console of its own; borrow the parent's when started from a shell.
        AttachConsole(0xFFFFFFFF);

        // Without a window, OnMainWindowClose would shut the process down immediately.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var configPath = args
            .SkipWhile(a => !a.Equals("--config", StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault();

        var config = WatchConfig.Load(configPath);
        var exitCode = HeadlessRunner.RunAsync(config).GetAwaiter().GetResult();
        Shutdown(exitCode);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}",
            "Zandronum Watcher",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
