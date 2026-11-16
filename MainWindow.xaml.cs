using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Shortcuts.Helpers;
using Shortcuts.Models;
using Shortcuts.Services;

namespace Shortcuts;

public partial class MainWindow : Window
{
    private const string ReorderFormat = "ShortcutIndex";
    private const string DragHandleTag = "DragHandle";   // matches the Tag on the handle in MainWindow.xaml
    private const int ErrorCancelled = 1223;             // Win32 ERROR_CANCELLED

    // How long after losing the focus the window still counts as the one the user was looking at.
    // A click on the notification icon takes the focus away a moment before the click is reported.
    private const int JustNowMilliseconds = 400;

    private readonly ShortcutStore _store = new();
    private readonly ObservableCollection<Shortcut> _shortcuts = [];

    // Where the left button last went down in the list: which row (-1 if none), and whether on its drag handle.
    private Point _pressPoint;
    private int _pressIndex = -1;
    private bool _pressOnHandle;

    private long _deactivatedAt;
    private bool _refreshingIcons;
    private readonly Size _defaultSize;

    public MainWindow()
    {
        InitializeComponent();
        _defaultSize = new Size(Width, Height);

        foreach (var s in _store.Load())
            _shortcuts.Add(s);
        RefreshIcons();

        ShortcutsList.ItemsSource = _shortcuts;
        _shortcuts.CollectionChanged += (_, _) => UpdateEmptyState();
        UpdateEmptyState();

        RestoreWindowPosition();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Deactivated += (_, _) => _deactivatedAt = Environment.TickCount64;
        IsVisibleChanged += MainWindow_IsVisibleChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ThemeHelper.ApplyTitleBarTheme(this, ThemeHelper.IsDarkMode());
    }

    // --- Showing and hiding. The app stays loaded, so the window is put away rather than closed. ---

    /// <summary>Set when the app is really exiting. Until then, closing the window only hides it.</summary>
    public bool IsExiting { get; set; }

    /// <summary>True if the user is working in the window, or was until a moment ago.</summary>
    public bool WasActiveJustNow =>
        IsActive || Environment.TickCount64 - _deactivatedAt < JustNowMilliseconds;

    public void BringToFront()
    {
        if (IsExiting)
            return;

        if (!IsVisible)
            Show();

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        // With a message box or a file picker open, that dialog is what the user has to deal with.
        var dialog = WindowHelper.GetBlockingDialog(new WindowInteropHelper(this).Handle);
        if (dialog != IntPtr.Zero)
        {
            WindowHelper.Activate(dialog);
            return;
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    /// <summary>Shows the window at its default size in the middle of the main screen.</summary>
    public void ResetPosition()
    {
        if (IsExiting)
            return;

        WindowState = WindowState.Normal;
        Width = _defaultSize.Width;
        Height = _defaultSize.Height;

        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + (area.Height - Height) / 2;

        BringToFront();
    }

    public void HideToTray()
    {
        if (!IsVisible)
            return;

        SaveWindowSettings();
        Hide();

        // The next time it is shown it should look freshly opened, not the way it was left.
        ShortcutsList.SelectedIndex = -1;
        if (_shortcuts.Count > 0)
            ShortcutsList.ScrollIntoView(_shortcuts[0]);
    }

    private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // The app can stay loaded for days. A target that was unreachable when its icon was last
        // looked up, such as a network drive at sign-in, gets its real icon the next time around.
        if (IsVisible)
            RefreshIcons();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideToTray();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void RestoreWindowPosition()
    {
        var reset = ((App)Application.Current).ResetPosition;
        var settings = reset ? null : _store.LoadWindowSettings();

        if (settings != null &&
            !double.IsNaN(settings.Left) && !double.IsNaN(settings.Top) &&
            !double.IsNaN(settings.Width) && !double.IsNaN(settings.Height))
        {
            Left = settings.Left;
            Top = settings.Top;
            Width = settings.Width;
            Height = settings.Height;

            if (settings.IsMaximized)
                WindowState = WindowState.Maximized;

            // Ensure the window is at least partially visible on the virtual screen
            var vsLeft = SystemParameters.VirtualScreenLeft;
            var vsTop = SystemParameters.VirtualScreenTop;
            var vsRight = vsLeft + SystemParameters.VirtualScreenWidth;
            var vsBottom = vsTop + SystemParameters.VirtualScreenHeight;

            if (Left + Width < vsLeft || Left > vsRight ||
                Top + Height < vsTop || Top > vsBottom)
            {
                CenterOnScreen();
            }
        }
        else
        {
            CenterOnScreen();
        }
    }

    private void CenterOnScreen()
    {
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (IsExiting)
            return;

        // The X button puts the window away. The app itself ends from the notification icon's menu.
        e.Cancel = true;
        HideToTray();
    }

    public void SaveWindowSettings()
    {
        // A window that was never shown has no position worth saving.
        if (!IsLoaded)
            return;

        var settings = new Models.WindowSettings
        {
            IsMaximized = WindowState == WindowState.Maximized
        };

        if (WindowState == WindowState.Normal)
        {
            settings.Left = Left;
            settings.Top = Top;
            settings.Width = Width;
            settings.Height = Height;
        }
        else
        {
            settings.Left = RestoreBounds.Left;
            settings.Top = RestoreBounds.Top;
            settings.Width = RestoreBounds.Width;
            settings.Height = RestoreBounds.Height;
        }

        _store.SaveWindowSettings(settings);
    }

    private void UpdateEmptyState()
    {
        EmptyMessage.Visibility = _shortcuts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // --- Adding and deleting ---

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select folders to add as shortcuts",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) == true)
            AddShortcuts(dialog.FolderNames);
    }

    private void AddFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select files to add as shortcuts",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) == true)
            AddShortcuts(dialog.FileNames);
    }

    /// <summary>Adds a shortcut for each path, at the end of the list or starting at <paramref name="insertAt"/>.</summary>
    private void AddShortcuts(IEnumerable<string> paths, int? insertAt = null)
    {
        var index = insertAt ?? _shortcuts.Count;
        var added = new List<Shortcut>();

        foreach (var path in paths)
        {
            var shortcut = new Shortcut
            {
                Name = DisplayName(path),
                Path = path,
                Kind = Directory.Exists(path) ? ShortcutKind.Folder : ShortcutKind.File
            };
            _shortcuts.Insert(index++, shortcut);
            added.Add(shortcut);
        }

        if (added.Count == 0)
            return;

        _store.Save(_shortcuts);
        LoadIcons(added);
        ShortcutsList.ScrollIntoView(added[^1]);
    }

    private static string DisplayName(string path)
    {
        // Explorer never shows the extension of its own shortcut files, so leave it off here too.
        var extension = Path.GetExtension(path);
        var isShellLink = extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
                          extension.Equals(".url", StringComparison.OrdinalIgnoreCase);

        var name = isShellLink ? Path.GetFileNameWithoutExtension(path) : Path.GetFileName(path);

        // A drive root such as C:\ has no file name.
        return string.IsNullOrEmpty(name) ? path : name;
    }

    /// <summary>Looks up every row's icon again. Does nothing if the last such round is still running.</summary>
    private void RefreshIcons()
    {
        if (_refreshingIcons || _shortcuts.Count == 0)
            return;

        _refreshingIcons = true;
        LoadIcons(_shortcuts.ToList(), () => _refreshingIcons = false);
    }

    private void LoadIcons(IReadOnlyList<Shortcut> shortcuts, Action? whenDone = null)
    {
        // Icon lookups read the disk and can stall on an unreachable network path, so they run
        // off the UI thread. The shell expects to be called from an STA thread.
        var thread = new Thread(() =>
        {
            foreach (var shortcut in shortcuts)
            {
                try
                {
                    var icon = ShellIconHelper.GetIcon(shortcut.Path, shortcut.Kind == ShortcutKind.Folder);
                    if (icon != null)
                        Dispatcher.BeginInvoke(() => shortcut.Icon = icon);
                }
                catch (Exception)
                {
                    // An icon is decoration. A row without one still works, so never let this take the app down.
                }
            }

            if (whenDone != null)
                Dispatcher.BeginInvoke(whenDone);
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is Shortcut shortcut)
        {
            _shortcuts.Remove(shortcut);
            _store.Save(_shortcuts);
        }
    }

    // --- Opening ---

    /// <summary>Opens the shortcut's target. Returns false, after telling the user why, if it could not be opened.</summary>
    private bool Open(Shortcut shortcut)
    {
        var path = shortcut.Path;
        var isFolder = Directory.Exists(path);

        if (!isFolder && !File.Exists(path))
        {
            var (what, title) = shortcut.Kind == ShortcutKind.File
                ? ("file", "File Not Found")
                : ("folder", "Folder Not Found");

            MessageBox.Show(this, $"The {what} no longer exists:\n{path}", title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };

        // Start a file in its own folder, as Explorer does, so scripts and programs find what sits next to them.
        if (!isFolder)
            startInfo.WorkingDirectory = Path.GetDirectoryName(path) ?? string.Empty;

        try
        {
            Process.Start(startInfo)?.Dispose();
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            // The user backed out of a Windows prompt, such as the elevation prompt. Nothing to report.
            return false;
        }
        catch (Win32Exception ex)
        {
            MessageBox.Show(this, $"Could not open:\n{path}\n\n{ex.Message}", "Could Not Open",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    // --- Mouse handling: click a row to open it, drag its handle to reorder ---

    private void ShortcutsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;

        _pressPoint = e.GetPosition(null);
        _pressOnHandle = IsOnDragHandle(source);

        // A press on the delete button belongs to the button, not to the row.
        _pressIndex = IsOnButton(source) ? -1 : GetRowIndex(source) ?? -1;
    }

    private void ShortcutsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var pressIndex = _pressIndex;
        var pressOnHandle = _pressOnHandle;
        _pressIndex = -1;
        _pressOnHandle = false;

        // The handle is only for dragging, so a click on it does not open the row.
        if (pressIndex < 0 || pressIndex >= _shortcuts.Count || pressOnHandle)
            return;

        // Count it as a click only if the button comes up on the row it went down on.
        var source = e.OriginalSource as DependencyObject;
        if (IsOnDragHandle(source) || IsOnButton(source) || GetRowIndex(source) != pressIndex)
            return;

        var shortcut = _shortcuts[pressIndex];
        var keepWindowOpen = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        // Let the list finish handling this click before a message box or hiding the window interrupts it.
        Dispatcher.BeginInvoke(() =>
        {
            if (Open(shortcut) && !keepWindowOpen)
                HideToTray();
        });
    }

    private void ShortcutsList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !_pressOnHandle || _pressIndex < 0)
            return;

        var diff = _pressPoint - e.GetPosition(null);

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var sourceIndex = _pressIndex;
        _pressIndex = -1;
        _pressOnHandle = false;

        DragDrop.DoDragDrop(ShortcutsList, new DataObject(ReorderFormat, sourceIndex), DragDropEffects.Move);
    }

    // --- Drops: one of our own rows being reordered, or files and folders dragged in from Explorer ---

    private static DragDropEffects DropEffect(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(ReorderFormat))
            return DragDropEffects.Move;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            return e.AllowedEffects.HasFlag(DragDropEffects.Link) ? DragDropEffects.Link : DragDropEffects.Copy;

        return DragDropEffects.None;
    }

    private void ShortcutsList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DropEffect(e);
        e.Handled = true;
    }

    private void ShortcutsList_Drop(object sender, DragEventArgs e)
    {
        // Always report the effect back. Left alone it can read as "moved", and Explorer deletes
        // the originals of files it believes were moved.
        e.Effects = DropEffect(e);
        e.Handled = true;

        var targetIndex = GetRowIndex(e.OriginalSource as DependencyObject);

        if (e.Data.GetDataPresent(ReorderFormat))
        {
            var sourceIndex = (int)e.Data.GetData(ReorderFormat)!;
            MoveShortcut(sourceIndex, targetIndex ?? _shortcuts.Count - 1);
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddShortcuts(paths, targetIndex);
        }
    }

    private void MoveShortcut(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= _shortcuts.Count)
            return;
        if (targetIndex < 0)
            targetIndex = _shortcuts.Count - 1;
        if (sourceIndex == targetIndex)
            return;

        _shortcuts.Move(sourceIndex, targetIndex);
        _store.Save(_shortcuts);
    }

    // --- Working out what in the list a mouse event landed on ---

    private int? GetRowIndex(DependencyObject? source) =>
        FindAncestor(source, d => d is ListBoxItem) is ListBoxItem item
            ? ShortcutsList.ItemContainerGenerator.IndexFromContainer(item)
            : null;

    private bool IsOnDragHandle(DependencyObject? source) =>
        FindAncestor(source, d => d is FrameworkElement { Tag: DragHandleTag }) != null;

    private bool IsOnButton(DependencyObject? source) =>
        FindAncestor(source, d => d is Button) != null;

    /// <summary>Walks up from the element under the mouse towards the list and returns the first match.</summary>
    private DependencyObject? FindAncestor(DependencyObject? source, Func<DependencyObject, bool> match)
    {
        while (source != null && source != ShortcutsList)
        {
            if (match(source))
                return source;

            source = source is Visual
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }
        return null;
    }
}
