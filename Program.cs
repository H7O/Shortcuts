using System.Runtime.CompilerServices;
using Shortcuts.Helpers;

namespace Shortcuts;

public static class Program
{
    /// <summary>Start without showing the window. Used by "Start with Windows".</summary>
    public const string HiddenArgument = "--hidden";

    /// <summary>Ask the copy that is already running to exit.</summary>
    public const string ExitArgument = "--exit";

    /// <summary>Show the window at its default size in the middle of the screen, for when it has gone astray.</summary>
    public const string ResetArgument = "--reset";
    private const string ShortResetArgument = "-r";

    [STAThread]
    public static int Main(string[] args)
    {
        var exit = HasArgument(args, ExitArgument);

        // The app stays loaded once started, so most launches find a copy already running and only
        // have to pass on a request. That path does not touch WPF, which keeps it quick.
        if (!SingleInstanceHelper.TryAcquire())
        {
            if (exit)
                SingleInstanceHelper.AskRunningCopyToExit();
            else if (WantsReset(args))
                SingleInstanceHelper.AskRunningCopyToReset();
            else if (!HasArgument(args, HiddenArgument))
                SingleInstanceHelper.AskRunningCopyToShow();
            return 0;
        }

        if (exit)
        {
            // Nothing was running, so there is nothing to do.
            SingleInstanceHelper.Release();
            return 0;
        }

        return RunApp();
    }

    public static bool HasArgument(string[] args, string name) =>
        Array.Exists(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static bool WantsReset(string[] args) =>
        HasArgument(args, ResetArgument) || HasArgument(args, ShortResetArgument);

    // Kept out of Main so that WPF is not loaded until this is called.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunApp()
    {
        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
