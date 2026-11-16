using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Shortcuts.Helpers;

/// <summary>
/// The app's icon in the notification area of the taskbar, next to the clock.
/// It reports clicks through a window that the caller owns: pass that window's messages
/// to <see cref="HandleMessage"/>.
/// </summary>
public sealed unsafe partial class TrayIcon : IDisposable
{
    private const uint IconId = 1;
    private const int CallbackMessage = 0x8000 + 1;   // WM_APP + 1

    // At sign-in the taskbar may not be ready to take icons yet, so a failed add is tried again for a while.
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);
    private const int MaxRetries = 30;

    private readonly IntPtr _window;
    private readonly string _tooltip;
    private readonly IntPtr _icon;        // the small one shown in the notification area
    private readonly IntPtr _largeIcon;   // the one shown on notifications
    private readonly bool _ownsIcons;
    private readonly uint _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

    private DispatcherTimer? _retryTimer;
    private int _retries;
    private long _lastSelected;
    private bool _disposed;

    /// <summary>True while the icon is in the notification area.</summary>
    public bool IsShown { get; private set; }

    /// <summary>The icon was clicked, or chosen with the keyboard.</summary>
    public event Action? Selected;

    /// <summary>The icon was right-clicked, or its menu was asked for with the keyboard.</summary>
    public event Action? MenuRequested;

    /// <summary>The note shown by <see cref="ShowNote"/> was clicked.</summary>
    public event Action? NoteClicked;

    public TrayIcon(IntPtr window, string tooltip)
    {
        _window = window;
        _tooltip = tooltip;

        // The app icon is built into the exe. Take it at the two sizes Windows asks for.
        _icon = LoadAppIcon(GetSystemMetrics(SM_CXSMICON));
        _largeIcon = LoadAppIcon(GetSystemMetrics(SM_CXICON));
        _ownsIcons = _icon != IntPtr.Zero && _largeIcon != IntPtr.Zero;
        if (!_ownsIcons)
            _icon = _largeIcon = LoadIcon(IntPtr.Zero, IDI_APPLICATION);   // the plain Windows one, rather than none

        Add();
    }

    /// <summary>Handles a message sent to the window given to the constructor. Returns true if it was the icon's.</summary>
    public bool HandleMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == _taskbarCreatedMessage)
        {
            // Explorer restarted and lost every icon, so put this one back.
            Add();
            return false;
        }

        if (msg != CallbackMessage)
            return false;

        switch ((int)(lParam.ToInt64() & 0xFFFF))
        {
            case NIN_SELECT:
            case NIN_KEYSELECT:
                // A double-click arrives as two of these, and so does the Enter key. Count each pair once.
                var now = Environment.TickCount64;
                if (now - _lastSelected > GetDoubleClickTime())
                {
                    _lastSelected = now;
                    Selected?.Invoke();
                }
                break;

            case WM_CONTEXTMENU:
                MenuRequested?.Invoke();
                break;

            case NIN_BALLOONUSERCLICK:
                NoteClicked?.Invoke();
                break;
        }
        return true;
    }

    /// <summary>Shows a Windows notification that comes from the icon. Returns false if Windows refused it.</summary>
    public bool ShowNote(string title, string text)
    {
        if (!IsShown)
            return false;

        var data = NewData(NIF_INFO);
        Copy(title, data.szInfoTitle, 64);
        Copy(text, data.szInfo, 256);
        data.dwInfoFlags = NIIF_USER | NIIF_LARGE_ICON;
        data.hBalloonIcon = _largeIcon;
        return Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _retryTimer?.Stop();

        var data = NewData(0);
        Shell_NotifyIcon(NIM_DELETE, ref data);
        IsShown = false;

        if (_ownsIcons)
        {
            DestroyIcon(_icon);
            DestroyIcon(_largeIcon);
        }
    }

    private void Add()
    {
        if (_disposed)
            return;

        IsShown = TryAdd();

        if (IsShown)
        {
            _retryTimer?.Stop();
            return;
        }

        if (_retryTimer == null)
        {
            _retryTimer = new DispatcherTimer { Interval = RetryInterval };
            _retryTimer.Tick += (_, _) =>
            {
                if (++_retries >= MaxRetries)
                    _retryTimer.Stop();
                Add();
            };
        }

        if (_retries < MaxRetries)
            _retryTimer.Start();
    }

    private bool TryAdd()
    {
        var data = NewData(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = _icon;
        Copy(_tooltip, data.szTip, 128);

        // Adding fails if the icon is already there, which happens when Explorer announces a new
        // taskbar without having lost the old icons. Updating the existing one is then enough.
        if (!Shell_NotifyIcon(NIM_ADD, ref data) && !Shell_NotifyIcon(NIM_MODIFY, ref data))
            return false;

        // Version 4 is what makes Windows report a click as NIN_SELECT and a menu request as WM_CONTEXTMENU.
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, ref data);
        return true;
    }

    private static IntPtr LoadAppIcon(int size) =>
        LoadImage(GetModuleHandle(null), IDI_APPLICATION, IMAGE_ICON, size, size, 0);

    private NOTIFYICONDATAW NewData(uint flags) => new()
    {
        cbSize = (uint)sizeof(NOTIFYICONDATAW),
        hWnd = _window,
        uID = IconId,
        uFlags = flags
    };

    /// <summary>Copies text into one of the fixed-size, zero-terminated buffers of the struct below.</summary>
    private static void Copy(string text, ushort* destination, int capacity)
    {
        var length = Math.Min(text.Length, capacity - 1);
        for (var i = 0; i < length; i++)
            destination[i] = text[i];
        destination[length] = 0;
    }

    // --- Win32 ---

    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4;
    private const uint NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_INFO = 0x10, NIF_SHOWTIP = 0x80;
    private const uint NIIF_USER = 0x4, NIIF_LARGE_ICON = 0x20;
    private const uint NOTIFYICON_VERSION_4 = 4;
    private const int NIN_SELECT = 0x400, NIN_KEYSELECT = 0x401, NIN_BALLOONUSERCLICK = 0x405;
    private const int WM_CONTEXTMENU = 0x7B;
    private const int SM_CXICON = 11, SM_CXSMICON = 49;
    private const uint IMAGE_ICON = 1;
    private static readonly IntPtr IDI_APPLICATION = 32512;   // also the id the compiler gives the exe's own icon

    // The text buffers are WCHAR arrays. They are declared as ushort so the struct can be passed as it is.
    [StructLayout(LayoutKind.Sequential)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        public fixed ushort szTip[128];
        public uint dwState;
        public uint dwStateMask;
        public fixed ushort szInfo[256];
        public uint uVersion;
        public fixed ushort szInfoTitle[64];
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATAW data);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterWindowMessage(string message);

    [LibraryImport("user32.dll")]
    private static partial uint GetDoubleClickTime();

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW")]
    private static partial IntPtr LoadImage(IntPtr instance, IntPtr name, uint type, int width, int height, uint load);

    [LibraryImport("user32.dll", EntryPoint = "LoadIconW")]
    private static partial IntPtr LoadIcon(IntPtr instance, IntPtr name);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr icon);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetModuleHandle(string? moduleName);
}
