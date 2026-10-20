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
    private const int WmNull = 0x0000;
    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmRButtonDown = 0x0204;
    private const int WmRButtonUp = 0x0205;
    private const int WmContextMenu = 0x007B;
    private const int NinSelect = 0x0400;
    private const int NinKeySelect = 0x0401;

    private const uint NimAdd = 0x0;
    private const uint NimModify = 0x1;
    private const uint NimDelete = 0x2;
    private const uint NimSetVersion = 0x4;
    private const uint NotifyIconVersion4 = 4;

    private const int NifMessage = 0x1;
    private const int NifIcon = 0x2;
    private const int NifTip = 0x4;

    private readonly IntPtr _windowHandle;
    private readonly uint _callbackMessage;
    private readonly uint _taskbarCreatedMessage;
    private readonly Icon _icon;
    private readonly IntPtr _iconHandle;
    private NotifyIconData _data;
    private bool _added;
    private bool _version4;
    private bool _disposed;

    public TrayIconService(IntPtr windowHandle, string tooltip)
    {
        _windowHandle = windowHandle;
        _callbackMessage = WmApp + 1;
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
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
        Diagnostics.Log($"NIM_ADD added={_added} hwnd=0x{windowHandle.ToInt64():X} callback=0x{_callbackMessage:X} err={Marshal.GetLastWin32Error()}");

        if (_added)
        {
            ApplyVersion4();
        }
    }

    /// <summary>The private window message the shell uses to call us back.</summary>
    public uint CallbackMessage => _callbackMessage;

    /// <summary>Fires when the shell broadcasts TaskbarCreated (Explorer restarted).</summary>
    public uint TaskbarCreatedMessage => _taskbarCreatedMessage;

    public bool IsRegistered => _added;

    /// <summary>Raised when the user activates the icon (click, double-click or keyboard).</summary>
    public event EventHandler? Activated;

    /// <summary>Raised for the context-menu request.</summary>
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

    /// <summary>Re-adds the icon after Explorer restarted and dropped every tray icon.</summary>
    public void ReRegister()
    {
        if (_disposed)
        {
            return;
        }

        _data.uFlags = NifMessage | NifIcon | NifTip;
        _added = Shell_NotifyIcon(NimAdd, ref _data);
        Diagnostics.Log($"NIM_ADD (re-register) added={_added}");

        if (_added)
        {
            ApplyVersion4();
        }
    }

    /// <summary>
    /// Opts into NOTIFYICON_VERSION_4 so the callback layout is well defined on every Windows
    /// version, and keyboard activation arrives as NIN_SELECT/NIN_KEYSELECT.
    /// </summary>
    private void ApplyVersion4()
    {
        var version = new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _windowHandle,
            uID = 1,
            uVersion = NotifyIconVersion4,
        };

        _version4 = Shell_NotifyIcon(NimSetVersion, ref version);
        Diagnostics.Log($"NIM_SETVERSION ok={_version4} err={Marshal.GetLastWin32Error()}");
    }

    /// <summary>
    /// Decodes a tray callback. Returns true when the message belonged to us.
    /// </summary>
    /// <remarks>
    /// There are two layouts in the wild, and the choice is not reliably signalled: legacy puts
    /// the mouse message in <c>wParam</c> and coordinates in <c>lParam</c>, while version 4
    /// swaps them. This decodes whichever operand actually carries a mouse message.
    /// <para>
    /// Version 4 also still emits the legacy <c>WM_LBUTTONUP</c> alongside <c>NIN_SELECT</c>,
    /// so both must not be treated as activations — doing that toggled the overlay twice per
    /// click and made the icon look dead. Once version 4 is confirmed, only the version-4
    /// messages are acted on.
    /// </para>
    /// </remarks>
    public bool TryHandleMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != _callbackMessage)
        {
            return false;
        }

        var fromWParam = (int)(wParam.ToInt64() & 0xFFFF);
        var fromLParam = (int)(lParam.ToInt64() & 0xFFFF);
        var mouseMessage = IsMouseMessage(fromWParam) ? fromWParam : fromLParam;

        if (_version4)
        {
            switch (mouseMessage)
            {
                case NinSelect:
                case NinKeySelect:
                    Activated?.Invoke(this, EventArgs.Empty);
                    break;

                case WmContextMenu:
                    ContextMenuRequested?.Invoke(this, EventArgs.Empty);
                    break;

                case WmLButtonUp:
                case WmLButtonDblClk:
                case WmRButtonUp:
                case WmRButtonDown:
                    // Duplicate of the NIN_SELECT / WM_CONTEXTMENU already handled above.
                    break;
            }

            return true;
        }

        switch (mouseMessage)
        {
            case WmLButtonUp:
            case WmLButtonDblClk:
                Activated?.Invoke(this, EventArgs.Empty);
                break;

            case WmRButtonUp:
            case WmRButtonDown:
                ContextMenuRequested?.Invoke(this, EventArgs.Empty);
                break;
        }

        return true;
    }

    private static bool IsMouseMessage(int value) => value is
        WmMouseMove or WmLButtonDown or WmLButtonUp or WmLButtonDblClk
        or WmRButtonDown or WmRButtonUp or WmContextMenu or NinSelect or NinKeySelect;

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

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