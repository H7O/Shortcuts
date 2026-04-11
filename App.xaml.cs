using System.Windows;
using Shortcuts.Helpers;

namespace Shortcuts;

public partial class App : Application
{
    public bool ResetPosition { get; private set; }

    private void App_Startup(object sender, StartupEventArgs e)
    {
        if (!SingleInstanceHelper.TryAcquire())
        {
            SingleInstanceHelper.NotifyExistingInstance();
            Shutdown();
            return;
        }

        ResetPosition = e.Args.Any(a =>
            a.Equals("--reset", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-r", StringComparison.OrdinalIgnoreCase));

        ThemeHelper.ApplyTheme(ThemeHelper.IsDarkMode());

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        ThemeHelper.WatchForThemeChanges(window);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SingleInstanceHelper.Release();
        base.OnExit(e);
    }
}

