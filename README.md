<p align="center">
  <img src="Assets/icon.png" width="96" alt="Enigma Disk icon">
</p>

<h1 align="center">Enigma Disk</h1>

<p align="center">
  <b>Find out what is eating your disk space — and when it started.</b><br>
  A Windows app that tracks which folders grow over time and helps you clean up safely.
</p>

<p align="center">
  English · <a href="README.ru.md">Русский</a>
</p>

---

## Why

Disk space rarely disappears all at once. Windows updates, browser and messenger caches,
GPU shader caches and developer tools grow a little every day, until one morning the drive is full
and nothing looks guilty.

Tools like WizTree or WinDirStat show what is on the disk **right now**. Enigma Disk takes
**snapshots** and compares them, so it answers a different question: **which folders grew this week?**

## Features

- **Snapshots** — a fast multi-threaded scan records the size of every folder over 1 MB and the 300 largest files.
  A full scan of a 240 GB system drive with ~1 million files takes about a minute and a half the first time and around 20 seconds on repeat runs.
- **What grew** — compare with yesterday, a week ago, a month ago or the first snapshot.
  The list shows the folders that actually grew, not the whole chain of parents
  (`Users → user → AppData → …`), with a plain-language hint for well-known folders
  (shader caches, Windows updates, Telegram, NuGet, Gradle, Steam and more).
- **Folders & files** — browse any snapshot with sizes, share of the parent folder and change over the week,
  plus a list of the largest files.
- **Clean up** — only well-known caches and temporary files: Temp, browser caches, Discord and Teams,
  GPU shader caches, crash dumps, downloaded Windows updates, developer caches, Recycle Bin.
  Files in use are skipped, symlinks and junctions are never followed, nothing is deleted without confirmation.
- **Daily snapshots** via Windows Task Scheduler — no background service, low priority,
  catches up after the PC was off.
- **Accurate totals** — hard-linked files in the Windows folder (WinSxS / System32) are not counted twice,
  and cloud-only OneDrive / iCloud files are skipped.
- **Russian and English** interface, switchable on the fly.
- **Private** — everything stays in `%LOCALAPPDATA%\EnigmaDisk`. Nothing is sent anywhere.
  Scanning only reads file sizes and never changes the disk.
- Minimal dark design: black, gray, white and a yellow accent.

## Install

Download `EnigmaDiskSetup.exe` from [Releases](../../releases) and run it.

- Installs for the current user to `%LOCALAPPDATA%\Programs\Enigma Disk` — **no administrator rights needed**.
- Adds Start menu and (optionally) desktop shortcuts.
- Shows up in **Settings → Apps**, where it can be uninstalled.
- Running a newer installer updates the existing installation and keeps your snapshot history.

> The executable is not code-signed yet, so Windows SmartScreen may show
> “Windows protected your PC”. Click **More info → Run anyway**.

A portable `EnigmaDisk.exe` that needs no installation is also produced by the build (see below).

**Requirements:** Windows 10 or 11, 64-bit. The app is self-contained, no .NET installation required.

## Build from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

| Command | Result |
|---|---|
| `dotnet run` | Run the app for development |
| `dev.bat` | Debug build, writes `build.log` and launches the app |
| `build.bat` | Release build: `dist\EnigmaDiskSetup.exe` (installer) and `dist\app\EnigmaDisk.exe` (portable) |

The installer lives in the `Setup` folder. It targets .NET Framework 4.8, which ships with Windows 10 and 11,
so it adds only ~90 KB on top of the app it carries inside.

## Command line

| Arguments | What it does |
|---|---|
| `EnigmaDisk.exe --snapshot` | Snapshot the drives selected in Settings and exit, without a window (used by Task Scheduler) |
| `EnigmaDisk.exe --uninstall` | Remove the installed app: shortcuts, Apps entry, scheduled task and, optionally, the snapshot history |
| `EnigmaDisk.exe --uninstall --quiet` | Same, without questions (keeps the history) |

## Project structure

```
Core/        scanning, snapshot storage, growth comparison, cleanup, scheduler, translations
Controls/    custom drawn controls: progress bar, ring, line chart
Pages/       Overview, What grew, Folders & files, Clean up, Settings
Setup/       installer (.NET Framework 4.8 + WPF)
Theme.xaml   colors and styles shared by the app and the installer
```

## How growth detection works

Each snapshot stores folder sizes as a compact tree (≈300 KB gzip-compressed JSON per snapshot).
To compare two snapshots, Enigma Disk computes the size change of every folder and keeps only the
**most specific** ones: a folder is hidden if a single child explains at least 70% of its growth,
or if children that are listed themselves explain at least half of it.
That way you see `…\NVIDIA\DXCache +3 GB` instead of `C:\Users +3 GB`.

History is thinned out automatically: every snapshot from the last two weeks is kept,
then one per day, and after two months one per week.

## License

No license has been chosen yet. All rights reserved by the author until a license file is added.
