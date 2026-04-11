using System.Collections.ObjectModel;
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
    private readonly ShortcutStore _store = new();
    private readonly ObservableCollection<FolderShortcut> _shortcuts = [];

    private Point _dragStartPoint;
    private int _dragSourceIndex = -1;

    public MainWindow()
    {
        InitializeComponent();

        var loaded = _store.Load();
        foreach (var s in loaded)
            _shortcuts.Add(s);

        ShortcutsList.ItemsSource = _shortcuts;
        _shortcuts.CollectionChanged += (_, _) => UpdateEmptyState();
        UpdateEmptyState();

        RestoreWindowPosition();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);

        ThemeHelper.ApplyTitleBarTheme(this, ThemeHelper.IsDarkMode());
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)SingleInstanceHelper.BringToFrontMessage)
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
            handled = true;
        }
        return IntPtr.Zero;
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

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder to add as a shortcut"
        };

        if (dialog.ShowDialog(this) == true)
        {
            var path = dialog.FolderName;
            var name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(name))
                name = path;

            _shortcuts.Add(new FolderShortcut { Name = name, Path = path });
            _store.Save(_shortcuts);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is FolderShortcut shortcut)
        {
            _shortcuts.Remove(shortcut);
            _store.Save(_shortcuts);
        }
    }

    private void ShortcutsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ShortcutsList.SelectedItem is FolderShortcut shortcut)
        {
            if (Directory.Exists(shortcut.Path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = shortcut.Path,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show(
                    $"The folder no longer exists:\n{shortcut.Path}",
                    "Folder Not Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    // --- Drag and Drop reordering ---

    private void ShortcutsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);

        // Determine which item index we're clicking on
        if (GetListBoxItemIndex(e.OriginalSource as DependencyObject) is int index)
            _dragSourceIndex = index;
        else
            _dragSourceIndex = -1;
    }

    private void ShortcutsList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSourceIndex < 0)
            return;

        var pos = e.GetPosition(null);
        var diff = _dragStartPoint - pos;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        // Don't start drag if the click was on the delete button
        if (e.OriginalSource is DependencyObject dep && IsDeleteButton(dep))
            return;

        var data = new DataObject("ShortcutIndex", _dragSourceIndex);
        DragDrop.DoDragDrop(ShortcutsList, data, DragDropEffects.Move);
    }

    private void ShortcutsList_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("ShortcutIndex"))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void ShortcutsList_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("ShortcutIndex"))
            return;

        int sourceIndex = (int)e.Data.GetData("ShortcutIndex")!;
        int targetIndex = GetListBoxItemIndex(e.OriginalSource as DependencyObject) ?? _shortcuts.Count - 1;

        if (sourceIndex < 0 || sourceIndex >= _shortcuts.Count)
            return;
        if (targetIndex < 0)
            targetIndex = _shortcuts.Count - 1;
        if (sourceIndex == targetIndex)
            return;

        _shortcuts.Move(sourceIndex, targetIndex);
        _store.Save(_shortcuts);
    }

    private int? GetListBoxItemIndex(DependencyObject? source)
    {
        while (source != null && source != ShortcutsList)
        {
            if (source is ListBoxItem item)
                return ShortcutsList.ItemContainerGenerator.IndexFromContainer(item);
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private static bool IsDeleteButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Button btn && btn.ToolTip as string == "Delete shortcut")
                return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }
}
