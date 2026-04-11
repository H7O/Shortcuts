# Folder Shortcuts

A lightweight Windows desktop app for quick access to your favorite folders. Built with C# / WPF / .NET 10.

![Dark mode](https://img.shields.io/badge/dark%20mode-supported-blue)
![Single instance](https://img.shields.io/badge/single%20instance-yes-green)
![License](https://img.shields.io/badge/license-MIT-blue)

## Features

- **Add folder shortcuts** — pick any folder via the native Windows dialog
- **Open folders** — double-click a shortcut to open it in Explorer
- **Delete shortcuts** — remove any shortcut with one click
- **Drag-and-drop reordering** — arrange shortcuts in any order
- **Dark mode aware** — automatically follows the Windows theme, including the title bar
- **Single instance** — launching again brings the existing window to the foreground
- **Remembers window position and size** — restored on next launch
- **Portable single-file executable** — no installer needed

## Download

Grab the latest release from the [Releases page](https://github.com/H7O/Shortcuts/releases/latest):

- **Shortcuts-win-x64.zip** — for 64-bit Intel/AMD systems (most PCs)
- **Shortcuts-win-arm64.zip** — for ARM64 devices (e.g. Surface Pro X, Snapdragon laptops)

Extract the zip and run `Shortcuts.exe`. No installation required.

## Requirements

- Windows 10 or later
- No runtime installation needed — the app is fully self-contained

## Building from Source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
.\build.ps1
```

The output is a single portable executable at `deploy\release\Shortcuts.exe`.

## Usage

```powershell
# Run normally
.\Shortcuts.exe

# Reset window position and size to defaults
.\Shortcuts.exe --reset
```

## Data Storage

Shortcuts and window settings are stored in:

```
%APPDATA%\H7O\Shortcuts\
├── shortcuts.json    # folder shortcuts
└── window.json       # window position and size
```

## License

[MIT](LICENSE)
