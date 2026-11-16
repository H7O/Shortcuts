using System.Runtime.InteropServices;

namespace Shortcuts.Helpers;

public static partial class SingleInstanceHelper
{
    private const string MutexName = "H7O.Shortcuts.SingleInstance";

    private static Mutex? _mutex;

    // A second copy of the app broadcasts one of these to the copy that is already running.
    public static readonly uint BringToFrontMessage = RegisterWindowMessage("H7O.Shortcuts.BringToFront");
    public static readonly uint ExitMessage = RegisterWindowMessage("H7O.Shortcuts.Exit");
    public static readonly uint ResetMessage = RegisterWindowMessage("H7O.Shortcuts.Reset");

    public static bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
        }
        return createdNew;
    }

    public static void AskRunningCopyToShow() => AskToComeForward(BringToFrontMessage);

    /// <summary>Asks the running copy to show its window at the default size, in the middle of the screen.</summary>
    public static void AskRunningCopyToReset() => AskToComeForward(ResetMessage);

    private static void AskToComeForward(uint message)
    {
        // The user has just started this copy, so Windows lets it put a window in front. The running
        // copy has no such right. Hand it over, or its window may only flash on the taskbar.
        AllowSetForegroundWindow(ASFW_ANY);
        PostMessage(HWND_BROADCAST, message, IntPtr.Zero, IntPtr.Zero);
    }

    public static void AskRunningCopyToExit()
    {
        PostMessage(HWND_BROADCAST, ExitMessage, IntPtr.Zero, IntPtr.Zero);
    }

    public static void Release()
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _mutex = null;
    }

    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);
    private const uint ASFW_ANY = 0xFFFFFFFF;

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterWindowMessage(string message);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(uint processId);
}
