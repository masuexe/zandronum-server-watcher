using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using Drawing = System.Drawing;

namespace ZandronumWatcher.App;

/// <summary>
/// System-tray icon for the overlay, so the watcher can keep running with no window on
/// screen. WPF has no tray support of its own, so this uses Shell_NotifyIcon directly rather
/// than pulling in a package for one icon.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const int WmApp = 0x8000;
    private const int WmLeftButtonUp = 0x0202;
    private const int WmRButtonUp = 0x0205;
    private const int WmLButtonDblClk = 0x0203;
    private const uint NimAdd = 0x0;
    private const uint NimModify = 0x1;
    private const uint NimDelete = 0x2;
    private const int NifMessage = 0x1;
    private const int NifIcon = 0x2;
    private const int NifTip = 0x4;

    private readonly IntPtr _windowHandle;
    private readonly uint _callbackMessage;
    private readonly Icon _icon;
    private readonly IntPtr _iconHandle;
    private NotifyIconData _data;
    private bool _added;
    private bool _disposed;

    public TrayIconService(IntPtr windowHandle, string tooltip)
    {
        _windowHandle = windowHandle;
        // A private message id: any value in the WM_APP range is safe.
        _callbackMessage = WmApp + 1;
        _icon = CreateIcon();
        _iconHandle = _icon.Handle;

        _data = new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = windowHandle,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = _callbackMessage,
            hIcon = _iconHandle,
            szTip = tooltip.Length >= 128 ? tooltip[..127] : tooltip,
            szInfo = "",
            szInfoTitle = "",
        };

        _added = Shell_NotifyIcon(NimAdd, ref _data);
    }

    /// <summary>Raised when the user double-clicks the tray icon.</summary>
    public event EventHandler? Activated;

    /// <summary>Raised for the context-menu request (right click).</summary>
    public event EventHandler? ContextMenuRequested;

    /// <summary>Updates the tooltip, e.g. with a live player count.</summary>
    public void SetTooltip(string text)
    {
        if (_disposed || !_added)
        {
            return;
        }

        _data.szTip = text.Length >= 128 ? text[..127] : text;
        _data.uFlags = NifTip;
        Shell_NotifyIcon(NimModify, ref _data);
    }

    /// <summary>Feeds window messages here; returns true when the message was ours.</summary>
    public bool TryHandleMessage(int msg, IntPtr wParam)
    {
        if (msg != _callbackMessage)
        {
            return false;
        }

        switch ((int)(wParam.ToInt64() & 0xFFFF))
        {
            case WmLButtonDblClk:
            case WmLeftButtonUp:
                Activated?.Invoke(this, EventArgs.Empty);
                break;
            case WmRButtonUp:
                ContextMenuRequested?.Invoke(this, EventArgs.Empty);
                break;
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_added)
        {
            Shell_NotifyIcon(NimDelete, ref _data);
            _added = false;
        }

        _icon.Dispose();
    }

    /// <summary>
    /// Draws the tray icon at runtime: a rounded square with a filled dot. Shipping a .ico
    /// would need a binary asset generated outside the build; drawing it keeps the repo free
    /// of opaque blobs and still renders correctly at 16x16.
    /// </summary>
    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            using var background = new SolidBrush(Color.FromArgb(255, 0x15, 0x18, 0x1D));
            graphics.FillEllipse(background, 1, 1, 29, 29);

            using var ring = new Pen(Color.FromArgb(255, 0x2E, 0x34, 0x3D), 2f);
            graphics.DrawEllipse(ring, 1, 1, 29, 29);

            using var dot = new SolidBrush(Color.FromArgb(255, 0x6F, 0xD0, 0x8C));
            graphics.FillEllipse(dot, 10, 10, 12, 12);
        }

        var handle = bitmap.GetHicon();
        try
        {
            // Clone via Icon.FromHandle so the handle can be released by the GC later.
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }
}
