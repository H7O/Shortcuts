using System.Runtime.InteropServices;

namespace Shortcuts.Helpers;

public static partial class SingleInstanceHelper
{
    private const string MutexName = "H7O.Shortcuts.SingleInstance";

    private static Mutex? _mutex;

    public static readonly uint BringToFrontMessage = RegisterWindowMessage("H7O.Shortcuts.BringToFront");

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

    public static void NotifyExistingInstance()
    {
        PostMessage(HWND_BROADCAST, BringToFrontMessage, IntPtr.Zero, IntPtr.Zero);
    }

    public static void Release()
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _mutex = null;
    }

    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterWindowMessage(string message);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
