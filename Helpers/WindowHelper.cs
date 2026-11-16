using System.Runtime.InteropServices;

namespace Shortcuts.Helpers;

public static partial class WindowHelper
{
    private const uint GW_ENABLEDPOPUP = 6;

    /// <summary>Makes the window the one the user is working in.</summary>
    public static void Activate(IntPtr window) => SetForegroundWindow(window);

    /// <summary>
    /// Returns the dialog that is currently blocking the window, such as a message box or a file
    /// picker, or zero if there is none.
    /// </summary>
    public static IntPtr GetBlockingDialog(IntPtr window)
    {
        var popup = GetWindow(window, GW_ENABLEDPOPUP);
        return popup == window ? IntPtr.Zero : popup;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr window);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetWindow(IntPtr window, uint command);
}
