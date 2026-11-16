using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using Shortcuts.Helpers;
using Shortcuts.Models;
using Shortcuts.Services;

namespace Shortcuts;

public partial class App : Application
{
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WM_ACTIVATE = 0x0006;
    private const int WA_INACTIVE = 0;

    public bool ResetPosition { get; private set; }

    private readonly ShortcutStore _store = new();
    private AppSettings _settings = new();

    private MainWindow? _window;
    private HwndSource? _messageWindow;
    private TrayIcon? _tray;
    private ContextMenu? _trayMenu;
    private MenuItem? _startWithWindowsItem;
    private bool _exiting;

    private void App_Startup(object sender, StartupEventArgs e)
    {
        ResetPosition = Program.WantsReset(e.Args);

        _settings = _store.LoadAppSettings();

        ThemeHelper.ApplyTheme(ThemeHelper.IsDarkMode());

        _window = new MainWindow();
        MainWindow = _window;
        _window.IsVisibleChanged += Window_IsVisibleChanged;

        // The notification icon and requests from a second copy of the app report to a window.
        // They get an invisible one of their own, because the main window does not exist as far
        // as Windows is concerned until it has been shown, and the app can start hidden.
        _messageWindow = new HwndSource(new HwndSourceParameters("Shortcuts messages")
        {
            WindowStyle = WS_POPUP,
            ExtendedWindowStyle = WS_EX_TOOLWINDOW,
            Width = 0,
            Height = 0
        });
        _messageWindow.AddHook(MessageWindowProc);

        _tray = new TrayIcon(_messageWindow.Handle, "Shortcuts");
        _tray.Selected += Tray_Selected;
        _tray.MenuRequested += ShowTrayMenu;
        _tray.NoteClicked += () => _window.BringToFront();

        if (!Program.HasArgument(e.Args, Program.HiddenArgument))
            _window.Show();

        ThemeHelper.WatchForThemeChanges(_window);
    }

    private IntPtr MessageWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)SingleInstanceHelper.BringToFrontMessage)
        {
            _window?.BringToFront();
            handled = true;
        }
        else if (msg == (int)SingleInstanceHelper.ResetMessage)
        {
            _window?.ResetPosition();
            handled = true;
        }
        else if (msg == (int)SingleInstanceHelper.ExitMessage)
        {
            ExitApp();
            handled = true;
        }
        else if (_tray?.HandleMessage(msg, wParam, lParam) == true)
        {
            handled = true;
        }
        return IntPtr.Zero;
    }

    // --- The notification icon ---

    private void Tray_Selected()
    {
        if (_window == null)
            return;

        // A click on the icon puts the window away only if the user was just looking at it.
        // A window that is open but buried under other windows comes to the front instead.
        if (_window.IsVisible && _window.WasActiveJustNow)
            _window.HideToTray();
        else
            _window.BringToFront();
    }

    private void ShowTrayMenu()
    {
        if (_trayMenu == null)
        {
            var open = new MenuItem { Header = "Open", FontWeight = FontWeights.SemiBold };
            open.Click += (_, _) => _window?.BringToFront();

            _startWithWindowsItem = new MenuItem { Header = "Start with Windows", IsCheckable = true };
            _startWithWindowsItem.Click += StartWithWindows_Click;

            var exit = new MenuItem { Header = "Exit" };
            exit.Click += (_, _) => ExitApp();

            _trayMenu = new ContextMenu
            {
                Style = (Style)FindResource("TrayMenu"),
                Placement = PlacementMode.MousePoint,
                Items = { open, _startWithWindowsItem, new Separator(), exit }
            };

            // A menu normally closes itself when an item is chosen, but only while it has hold of
            // the mouse, and it can fail to get that. Close it here as well, so it never lingers.
            _trayMenu.AddHandler(MenuItem.ClickEvent, new RoutedEventHandler((_, _) => _trayMenu.IsOpen = false));
        }

        _startWithWindowsItem!.IsChecked = StartupHelper.IsEnabled();

        // Closing it first makes a second right-click move the menu to the mouse.
        _trayMenu.IsOpen = false;
        _trayMenu.IsOpen = true;

        // The menu has to become the active window. Otherwise it would not notice the user clicking
        // somewhere else, and would stay open.
        if (PresentationSource.FromVisual(_trayMenu) is HwndSource popup)
        {
            popup.AddHook(TrayMenuProc);
            WindowHelper.Activate(popup.Handle);
        }
    }

    private IntPtr TrayMenuProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // A click anywhere else makes the menu's window inactive. WPF usually closes the menu at
        // that point by itself. This makes sure of it.
        if (msg == WM_ACTIVATE && (wParam.ToInt64() & 0xFFFF) == WA_INACTIVE && _trayMenu is { IsOpen: true })
            Dispatcher.BeginInvoke(() => _trayMenu.IsOpen = false);

        return IntPtr.Zero;
    }

    private void StartWithWindows_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StartupHelper.SetEnabled(_startWithWindowsItem!.IsChecked);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            MessageBox.Show($"Could not change the startup setting.\n\n{ex.Message}", "Shortcuts",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // --- Hiding and exiting ---

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_exiting || _window is not { IsVisible: false })
            return;

        if (_tray is not { IsShown: true })
        {
            // Without the icon there is nothing to bring the window back with, or to exit from.
            // Staying loaded would leave a process that nobody can see, so end here instead.
            ExitApp();
            return;
        }

        // Say once where the app went. If Windows refuses the note, try again at the next hide.
        if (!_settings.HideNoteShown &&
            _tray.ShowNote("Shortcuts is still running",
                "It stays loaded so that it opens at once. Click its icon near the clock to open it, " +
                "or right-click the icon to exit. The icon may be under the ^ arrow."))
        {
            _settings.HideNoteShown = true;
            _store.SaveAppSettings(_settings);
        }
    }

    private void ExitApp()
    {
        if (_exiting)
            return;
        _exiting = true;

        if (_window != null)
        {
            _window.SaveWindowSettings();
            _window.IsExiting = true;
        }

        Shutdown();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);

        // Signing out or shutting down ends the app without going through ExitApp.
        if (!e.Cancel)
            _window?.SaveWindowSettings();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _messageWindow?.Dispose();
        SingleInstanceHelper.Release();
        base.OnExit(e);
    }
}
