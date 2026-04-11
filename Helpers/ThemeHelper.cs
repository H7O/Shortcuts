using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Shortcuts.Helpers;

public static partial class ThemeHelper
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryValueName = "AppsUseLightTheme";
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void ApplyTitleBarTheme(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        int value = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    public static bool IsDarkMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
        var value = key?.GetValue(RegistryValueName);
        return value is int i && i == 0;
    }

    public static void ApplyTheme(bool dark)
    {
        var res = Application.Current.Resources;

        if (dark)
        {
            res["WindowBackground"] = new SolidColorBrush(Color.FromRgb(32, 32, 32));
            res["ToolbarBackground"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
            res["ToolbarBorder"] = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            res["TextForeground"] = new SolidColorBrush(Color.FromRgb(240, 240, 240));
            res["SubtextForeground"] = new SolidColorBrush(Color.FromRgb(160, 160, 160));
            res["EmptyMessageForeground"] = new SolidColorBrush(Color.FromRgb(140, 140, 140));
            res["DeleteButtonForeground"] = new SolidColorBrush(Color.FromRgb(255, 100, 100));
            res["ButtonBackground"] = new SolidColorBrush(Color.FromRgb(55, 55, 55));
            res["ButtonForeground"] = new SolidColorBrush(Color.FromRgb(240, 240, 240));
            res["ButtonBorder"] = new SolidColorBrush(Color.FromRgb(80, 80, 80));
            res["ListItemSelectedBackground"] = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            res["ListItemHoverBackground"] = new SolidColorBrush(Color.FromRgb(50, 50, 50));
        }
        else
        {
            res["WindowBackground"] = new SolidColorBrush(Colors.White);
            res["ToolbarBackground"] = new SolidColorBrush(Color.FromRgb(240, 240, 240));
            res["ToolbarBorder"] = new SolidColorBrush(Color.FromRgb(204, 204, 204));
            res["TextForeground"] = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            res["SubtextForeground"] = new SolidColorBrush(Color.FromRgb(102, 102, 102));
            res["EmptyMessageForeground"] = new SolidColorBrush(Color.FromRgb(136, 136, 136));
            res["DeleteButtonForeground"] = new SolidColorBrush(Color.FromRgb(204, 0, 0));
            res["ButtonBackground"] = new SolidColorBrush(Color.FromRgb(221, 221, 221));
            res["ButtonForeground"] = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            res["ButtonBorder"] = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            res["ListItemSelectedBackground"] = new SolidColorBrush(Color.FromRgb(204, 228, 247));
            res["ListItemHoverBackground"] = new SolidColorBrush(Color.FromRgb(229, 243, 255));
        }
    }

    public static void WatchForThemeChanges(Window window)
    {
        SystemEvents.UserPreferenceChanged += (_, args) =>
        {
            if (args.Category == UserPreferenceCategory.General)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var dark = IsDarkMode();
                    ApplyTheme(dark);
                    ApplyTitleBarTheme(window, dark);
                });
            }
        };
    }
}
