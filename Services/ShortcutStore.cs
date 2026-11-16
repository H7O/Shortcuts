using System.IO;
using System.Text.Json;
using Shortcuts.Models;

namespace Shortcuts.Services;

public class ShortcutStore
{
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "H7O", "Shortcuts");

    private static readonly string FilePath = Path.Combine(AppDataFolder, "shortcuts.json");
    private static readonly string SettingsPath = Path.Combine(AppDataFolder, "window.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public List<Shortcut> Load()
    {
        if (!File.Exists(FilePath))
            return [];

        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<List<Shortcut>>(json, JsonOptions) ?? [];
    }

    public void Save(IEnumerable<Shortcut> shortcuts)
    {
        Directory.CreateDirectory(AppDataFolder);
        var json = JsonSerializer.Serialize(shortcuts.ToList(), JsonOptions);
        File.WriteAllText(FilePath, json);
    }

    public WindowSettings? LoadWindowSettings()
    {
        if (!File.Exists(SettingsPath))
            return null;

        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<WindowSettings>(json, JsonOptions);
    }

    public void SaveWindowSettings(WindowSettings settings)
    {
        Directory.CreateDirectory(AppDataFolder);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
