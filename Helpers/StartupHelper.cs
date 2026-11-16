using Microsoft.Win32;

namespace Shortcuts.Helpers;

/// <summary>Turns "Start with Windows" on and off for the current user.</summary>
public static class StartupHelper
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "H7O.Shortcuts";

    // Windows records here when someone switches a startup app off in Task Manager or in Settings.
    // The format is not documented. In practice the value is absent or starts with an even byte
    // when the app is allowed to start, and starts with an odd byte when it has been switched off.
    private const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    // The app starts hidden at sign-in. It is there to be ready, not to be looked at.
    private static string Command => $"\"{Environment.ProcessPath}\" {Program.HiddenArgument}";

    /// <summary>
    /// True if this copy of the app starts with Windows. A startup entry left behind by a copy
    /// in some other folder does not count, and neither does one that Windows has switched off.
    /// </summary>
    public static bool IsEnabled()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        if (run?.GetValue(ValueName) is not string command ||
            !command.Equals(Command, StringComparison.OrdinalIgnoreCase))
            return false;

        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath);
        var switchedOff = approved?.GetValue(ValueName) is byte[] { Length: > 0 } state && (state[0] & 1) == 1;
        return !switchedOff;
    }

    public static void SetEnabled(bool enabled)
    {
        using var run = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            run.SetValue(ValueName, Command);
        else
            run.DeleteValue(ValueName, throwOnMissingValue: false);

        // Either way, drop what Windows remembered. Turning it on here should win over an earlier
        // switch-off in Task Manager, and turning it off should leave nothing behind.
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, writable: true);
        approved?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
