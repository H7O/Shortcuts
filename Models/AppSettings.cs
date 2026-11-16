namespace Shortcuts.Models;

public class AppSettings
{
    /// <summary>
    /// True once the user has been told that closing the window leaves the app running
    /// in the notification area. The note is only worth showing once.
    /// </summary>
    public bool HideNoteShown { get; set; }
}
