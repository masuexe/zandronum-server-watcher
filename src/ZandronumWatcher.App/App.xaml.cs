using System.IO;
using System.Windows;
using System.Windows.Threading;
using ZandronumWatcher.Core.Configuration;

namespace ZandronumWatcher.App;

public partial class App : Application
{
    private const string MutexName = @"Local\ZandronumWatcher.SingleInstance";

    private Mutex? _mutex;

    /// <summary>Set when the process runs without a window.</summary>
    public static bool IsHeadless { get; private set; }

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

        if (TryGetPinArguments(e.Args, out var pinAddresses, out var unpin))
        {
            ConsoleHost.Attach();
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var configPath = GetConfigPath(e.Args) ?? WatchConfig.DefaultPath;
            var code = PinRunner.Run(configPath, pinAddresses, unpin);
            Shutdown(code);
            return;
        }

        if (e.Args.Any(a => a.Equals("--list", StringComparison.OrdinalIgnoreCase)))
        {
            RunList(e.Args);
            return;
        }

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

    private static bool TryGetPinArguments(string[] args, out List<string> addresses, out bool unpin)
    {
        addresses = [];
        unpin = args.Any(a => a.Equals("--unpin", StringComparison.OrdinalIgnoreCase));
        var flag = unpin ? "--unpin" : "--pin";

        if (!args.Any(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        addresses.AddRange(args
            .SkipWhile(a => !a.Equals(flag, StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .TakeWhile(a => !a.StartsWith("--", StringComparison.Ordinal)));

        return addresses.Count > 0;
    }

    private static string? GetConfigPath(string[] args) =>
        args.SkipWhile(a => !a.Equals("--config", StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault();

    private void RunList(string[] args)
    {
        ConsoleHost.Attach();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var master = args
            .SkipWhile(a => !a.Equals("--master", StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault();

        var showEmpty = args.Any(a => a.Equals("--all", StringComparison.OrdinalIgnoreCase));
        // Run on the thread pool: blocking the dispatcher thread would deadlock the awaits
        // inside the runner.
        var exitCode = Task.Run(() => DiscoverRunner.RunAsync(master, showEmpty)).GetAwaiter().GetResult();
        Shutdown(exitCode);
    }

    private void RunHeadless(string[] args)
    {
        ConsoleHost.Attach();

        // Without a window, OnMainWindowClose would shut the process down immediately.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var config = WatchConfig.Load(GetConfigPath(args));
        var exitCode = Task.Run(() => HeadlessRunner.RunAsync(config)).GetAwaiter().GetResult();
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