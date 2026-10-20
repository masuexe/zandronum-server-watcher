using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ZandronumWatcher.App;

/// <summary>
/// Wires the tray icon to a tiny message-only window.
/// </summary>
/// <remarks>
/// The icon needs a window handle to receive its callbacks. Using the overlay window would
/// break as soon as that window is hidden, so a dedicated message-only window owns it instead.
/// </remarks>
internal sealed class TrayHost : IDisposable
{
    private const int WmNull = 0x0000;

    private readonly HwndSource _source;
    private readonly TrayIconService _icon;
    private ContextMenu? _menu;

    public TrayHost()
    {
        var parameters = new HwndSourceParameters("ZandronumWatcherTray")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        _icon = new TrayIconService(_source.Handle, "Zandronum Watcher");
        Diagnostics.Log($"tray host created, registered={_icon.IsRegistered}");
        _icon.Activated += (_, _) => ToggleRequested?.Invoke(this, EventArgs.Empty);
        _icon.ContextMenuRequested += (_, _) => ShowMenu();
    }

    /// <summary>Raised when the user double-clicks the icon or picks "Show overlay".</summary>
    public event EventHandler? ToggleRequested;

    /// <summary>Raised when the user picks "Exit".</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Raised when the user picks "Add or remove servers…".</summary>
    public event EventHandler? ManageServersRequested;

    /// <summary>Raised when the user toggles "Start with Windows".</summary>
    public event EventHandler? AutostartRequested;

    public void SetTooltip(string text) => _icon.SetTooltip(text);

    private void ShowMenu()
    {
        // Rebuilt each time so the "Start with Windows" check mark reflects the registry
        // rather than a stale snapshot from when the menu was first opened.
        _menu = BuildMenu();
        _menu.IsOpen = true;
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x1F, 0x26)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xED, 0xED, 0xED)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x3D)),
            Padding = new Thickness(4),
        };

        menu.Items.Add(MenuItem("Show overlay", () => ToggleRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(MenuItem("Add or remove servers…", () => ManageServersRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new Separator());

        var startup = MenuItem(
            AutostartService.IsEnabled() ? "✓  Start with Windows" : "Start with Windows",
            () => AutostartRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(startup);

        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("Exit", () => ExitRequested?.Invoke(this, EventArgs.Empty)));
        return menu;
    }

    private static MenuItem MenuItem(string header, Action action)
    {
        var item = new MenuItem
        {
            Header = header,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0xED, 0xED, 0xED)),
            Padding = new Thickness(10, 5, 18, 5),
            Cursor = Cursors.Hand,
        };

        item.Click += (_, _) => action();
        return item;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmNull)
        {
            return IntPtr.Zero;
        }

        // Explorer restarted and dropped every tray icon; put ours back.
        if ((uint)msg == _icon.TaskbarCreatedMessage)
        {
            _icon.ReRegister();
            handled = true;
            return IntPtr.Zero;
        }

        if (_icon.TryHandleMessage(msg, wParam, lParam))
        {
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _icon.Dispose();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}