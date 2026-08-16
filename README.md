# compactWindows

<p align="center">
  <img src="https://i.imgur.com/vT1Tfi1.png" alt="compactWindows logo" />
</p>

<p align="center">
  <strong>A standalone GUI for Windows <code>compact.exe</code> — compress games and programs transparently to reclaim disk space.</strong>
</p>

<p align="center">
  <a href="https://github.com/Endymi0n74/compactWindows/releases/latest"><img alt="GitHub release" src="https://img.shields.io/github/v/release/Endymi0n74/compactWindows?label=latest%20release"></a>
  <a href="https://github.com/Endymi0n74/compactWindows/blob/master/LICENSE"><img alt="License" src="https://img.shields.io/badge/license-GPL--3.0-blue.svg"></a>
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%2010%2F11-blue">
  <img alt=".NET" src="https://img.shields.io/badge/.NET%20Framework-4.7-blue">
</p>

---

## What is it?

Windows ships a little-known tool called **Compact** that compresses files on disk and decompresses them on the fly at runtime. The result is transparent: your programs and games work exactly as before, but can shrink by up to 60%. On a modern CPU the added load is negligible, and on older spinning drives the smaller files can even make things load *faster*.

**compactWindows** is a small, open-source GUI that makes `compact.exe` easy to use — pick a folder, choose a compression level, and go.

## Installation

Download the standalone program from **[GitHub Releases](https://github.com/Endymi0n74/compactWindows/releases/latest)** and run it — no installation required.

## Uses

- **Compress program folders** — e.g. Adobe Photoshop: 1.71 GB → 886 MB
- **Compress game install folders** — e.g. Portal 2: 11.8 GB → 7.88 GB
- **Compress any other folder** on your computer

A community-maintained list of tested games and their measured savings lives in [`Wiki/WikiDB_Games`](Wiki/WikiDB_Games) and is used by the built-in *compression estimate* feature.

> The app intentionally only compresses folders and files. Whole drives and the Windows installation itself are off-limits — use the command-line tool for that.

## Features

- More accurate reporting than the built-in command-line tool
- Compression estimates for games based on community results
- Analyze whether a folder is already compressed
- Explorer right-click menu integration
- Optional shutdown / restart / sleep when a job finishes
- Live progress and a detailed console log
- Background folder scanning and asynchronous compression (the UI never freezes)

## Compression options

| Option     | Speed      | Compression | Notes                                        |
|------------|------------|-------------|----------------------------------------------|
| `XPRESS4K` | Fastest    | Lowest      | The Windows default — safest for old CPUs    |
| `XPRESS8K` | Balanced   | Good        | **Default in compactWindows**                |
| `XPRESS16K`| Slower     | Better      | Great for games and large programs           |
| `LZX`      | Slowest    | Best        | High CPU overhead — avoid for programs/games |

## Screenshots

<p align="left"><img src="https://i.imgur.com/f8yzhw2.png" alt="compactWindows screenshot 1"></p>
<p align="left"><img src="https://i.imgur.com/4yhwOGm.png" alt="compactWindows screenshot 2"></p>
<p align="left"><img src="https://i.imgur.com/7ip5SAA.png" alt="compactWindows screenshot 3"></p>

## Building from source

Requirements:

- Windows 10 or 11
- Visual Studio 2017 or newer (or MSBuild + the .NET Framework 4.7 Developer Pack)
- The .NET desktop development workload

Open `CompactGUI.sln`, build the **Release** configuration, and the standalone executable is produced at `WindowsApp1\bin\Release\CompactGUI.exe`.

## License

[GPL-3.0](LICENSE)

## Credits

compactWindows is a continuation of **CompactGUI** originally by [ImminentFate](https://github.com/ImminentFate/CompactGUI). The folder browser uses [Ookii.Dialogs](http://www.ookii.org/Software/Dialogs).
