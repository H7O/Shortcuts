using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace Shortcuts.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ShortcutKind>))]
public enum ShortcutKind
{
    // Folder is the default so shortcuts saved before files were supported load as folders.
    Folder,
    File
}

public class Shortcut : INotifyPropertyChanged
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public ShortcutKind Kind { get; set; }

    // Loaded in the background after the row is shown, so the row has to be told when it arrives.
    [JsonIgnore]
    public ImageSource? Icon
    {
        get;
        set
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
