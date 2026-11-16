# Shortcuts

A lightweight Windows desktop app for quick access to your favorite folders and files. Built with C# / WPF / .NET 11.

![Dark mode](https://img.shields.io/badge/dark%20mode-supported-blue)
![Single instance](https://img.shields.io/badge/single%20instance-yes-green)
![License](https://img.shields.io/badge/license-MIT-blue)

## Features

- **Folder and file shortcuts** — add them with the **Add Folder** and **Add File** buttons, or drag them in from Explorer
- **One-click open** — click a shortcut to open it; folders open in Explorer and files in their default app
- **Gets out of the way** — the window hides once the shortcut has opened; hold <kbd>Ctrl</kbd> while clicking to keep it open
- **Stays loaded** — closing the window, or pressing <kbd>Esc</kbd>, only hides it. The app keeps running in the notification area next to the clock, so it comes back at once
- **Notification icon** — click it to show or hide the window; right-click it for **Start with Windows** and **Exit**
- **Delete shortcuts** — remove any shortcut with one click
- **Drag-and-drop reordering** — drag a shortcut by the handle on its left edge
- **Dark mode aware** — automatically follows the Windows theme, including the title bar
- **Single instance** — launching again, for example from a pinned taskbar icon, brings the existing window to the foreground
- **Remembers window position and size** — restored on next launch
- **Portable single-file executable** — no installer needed

## Download

Grab the latest release from the [Releases page](https://github.com/H7O/Shortcuts/releases/latest):

- **Shortcuts-win-x64.zip** — for 64-bit Intel/AMD systems (most PCs)
- **Shortcuts-win-arm64.zip** — for ARM64 devices (e.g. Surface Pro X, Snapdragon laptops)

Extract the zip and run `Shortcuts.exe`. No installation required. The first time you run a new download, Windows can take several seconds to check the file before the window appears. Later starts are quick.

Windows puts new notification icons under the **^** arrow next to the clock. To keep the Shortcuts icon in view, open that panel and drag the icon onto the taskbar.

## Requirements

- Windows 10 or later
- No runtime installation needed — the app is fully self-contained

## Building from Source

Requires the [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0).

```powershell
.\build.ps1
```

The output is a single portable executable at `deploy\release\Shortcuts.exe`. If the app is running from that folder, the script asks it to exit first and starts it again afterwards.

## Usage

```powershell
# Run normally. If the app is already running, this shows its window.
.\Shortcuts.exe

# Start without showing the window. This is what "Start with Windows" uses.
.\Shortcuts.exe --hidden

# Put the window back at its default size in the middle of the screen
.\Shortcuts.exe --reset

# Ask the running app to exit
.\Shortcuts.exe --exit
```

**Start with Windows** is off by default. Turning it on adds one entry for the current user under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, and turning it off removes it. The entry points at wherever the exe is at that moment, so turn it off and on again after moving the exe.

## Data Storage

Shortcuts and window settings are stored in:

```
%APPDATA%\H7O\Shortcuts\
├── shortcuts.json    # your shortcuts
├── window.json       # window position and size
└── settings.json     # remembers that the "still running" note has been shown
```

## License

[MIT](LICENSE)
