using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ZandronumWatcher.App.Notifications;

/// <summary>
/// Registers the app with Windows so that toasts are delivered.
/// </summary>
/// <remarks>
/// An unpackaged desktop app gets no toasts unless a Start Menu shortcut carrying an
/// AppUserModelID exists. This was verified on Windows 11: without the shortcut
/// <c>ToastNotificationManager.CreateToastNotifier</c> reports success and silently drops the
/// notification, so the registration is a hard requirement, not a nicety.
/// </remarks>
internal static class AppRegistration
{
    /// <summary>The AUMID used for both the shortcut and the toaster.</summary>
    public const string AppUserModelId = "Masuexe.ZandronumWatcher";

    private const int CLSCTX_INPROC_SERVER = 1;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    public static void Register()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
            {
                return;
            }

            var shortcut = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs\Zandronum Watcher.lnk");

            ShortcutFactory.Create(shortcut, exe, AppUserModelId);
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        }
        catch (Exception)
        {
            // Notifications are a convenience; failing to register must not stop the watcher.
        }
    }

    private static class ShortcutFactory
    {
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLinkCoClass { }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey
        {
            public Guid FormatId;
            public uint PropertyId;
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PropertyKey pkey);
            void GetValue(ref PropertyKey key, out PropVariant pv);
            void SetValue(ref PropertyKey key, ref PropVariant pv);
            void Commit();
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PropVariant
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr pointerValue;
        }

        public static void Create(string shortcutPath, string targetPath, string appId)
        {
            // Not a static readonly field: SetValue takes the key by ref, which the compiler
            // forbids for static readonly fields.
            var appUserModelIdKey = new PropertyKey
            {
                FormatId = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
                PropertyId = 5,
            };

            var link = (IShellLinkW)new ShellLinkCoClass();
            link.SetPath(targetPath);
            link.SetWorkingDirectory(Path.GetDirectoryName(targetPath)!);
            link.SetDescription("Zandronum Watcher");

            var store = (IPropertyStore)link;
            var pv = new PropVariant { vt = 31, pointerValue = Marshal.StringToCoTaskMemUni(appId) };
            try
            {
                store.SetValue(ref appUserModelIdKey, ref pv);
                store.Commit();
            }
            finally
            {
                Marshal.FreeCoTaskMem(pv.pointerValue);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
            ((IPersistFile)link).Save(shortcutPath, true);
        }
    }
}
