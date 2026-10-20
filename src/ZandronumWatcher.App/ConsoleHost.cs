using System.IO;
using System.Runtime.InteropServices;

namespace ZandronumWatcher.App;

/// <summary>
/// Gives the WinExe a usable stdout for the CLI modes.
/// </summary>
/// <remarks>
/// The project is built as <c>WinExe</c> so double-clicking it never flashes a console window.
/// The side effect is that a launched-with-no-console process has no valid standard handles, so
/// <see cref="Console.WriteLine(string)"/> goes nowhere.</remarks>
internal static class ConsoleHost
{
    private const uint AttachParentProcess = 0xFFFFFFFF;
    private const int StdOutputHandle = -11;
    private const int StdErrorHandle = -12;
    private const uint FileTypeUnknown = 0x0000;
    private const uint FileTypeChar = 0x0002;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(IntPtr hFile);

    /// <summary>
    /// Makes sure the CLI modes can write somewhere sensible.
    /// </summary>
    /// <remarks>
    /// This deliberately does **not** call <c>AttachConsole</c> when stdout is already usable.
    /// Doing so unconditionally was a real bug: when the caller redirects output to a file or
    /// pipe (CI, `--list &gt; out.txt`, `Start-Process -RedirectStandardOutput`), attaching to
    /// the shell's console replaced the redirect, and the output silently vanished into the
    /// parent console even though the process exited 0.
    /// </remarks>
    public static void Attach()
    {
        var handle = GetStdHandle(StdOutputHandle);
        var type = GetFileType(handle);

        if (type == FileTypeChar)
        {
            // A real console already; it owns its code page, so leave it untouched.
            return;
        }

        if (type is 1 or 3)
        {
            // Redirected to a file or pipe. Keep the redirect, but force UTF-8: the default
            // here is the machine's ANSI code page, which turned "·" and "…" into mojibake.
            UseUtf8();
            return;
        }

        if (!AttachConsole(AttachParentProcess))
        {
            // Started from Explorer with no console at all; create one so the output is visible.
            AllocConsole();
        }

        Rebind();
    }

    private static void UseUtf8()
    {
        try
        {
            Console.OutputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
        catch (IOException)
        {
            // Encoding is cosmetic; never fail the command over it.
        }
    }

    private static void Rebind()
    {
        try
        {
            UseUtf8();
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
        catch (IOException)
        {
            // Nothing to bind to; the caller will simply see no output.
        }
    }
}