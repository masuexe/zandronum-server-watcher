using System.IO;
using System.Runtime.InteropServices;

namespace ZandronumWatcher.App;

/// <summary>
/// Gives the WinExe a usable stdout.
/// </summary>
/// <remarks>
/// The project is built as <c>WinExe</c> so double-clicking it never flashes a console window.
/// The side effect is that .NET wires <see cref="Console.Out"/> to a null writer, so CLI modes
/// would print nothing even when a shell is attached. Attaching to the parent console and
/// rebinding the standard streams restores normal behaviour.
/// </remarks>
internal static class ConsoleHost
{
    private const uint AttachParentProcess = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    /// <summary>Attaches to the launching shell's console and rebinds stdout/stderr.</summary>
    public static void Attach()
    {
        if (!AttachConsole(AttachParentProcess))
        {
            // No parent console (e.g. started from Explorer); give ourselves one so the
            // output is at least visible.
            AllocConsole();
        }

        Rebind();
    }

    private static void Rebind()
    {
        try
        {
            var stdout = Console.OpenStandardOutput();
            var writer = new StreamWriter(stdout) { AutoFlush = true };
            Console.SetOut(writer);

            var stderr = Console.OpenStandardError();
            var errorWriter = new StreamWriter(stderr) { AutoFlush = true };
            Console.SetError(errorWriter);
        }
        catch (IOException)
        {
            // Nothing to bind to; the caller will simply see no output.
        }
    }
}
