# Changelog

## v1.1.0 (2026-08-17)

### UI redesign

- Dark slate theme across the whole app: main window, progress page, console and dialogs (wiki estimate popup, shutdown dialog, About).
- New top bar with an **Admin** pill (restarts elevated), **Home / Watcher / Compression DB** navigation tabs and a settings gear.
- New left sidebar with an **Add Folder to Queue** button and the folder queue (click a queued folder to re-scan it).
- The Home page now shows the folder header, live stats (uncompressed size, contained files, compression status), a **Compression Summary** with before/after bars, and metric cards (**Space Saved**, **Files Compressed**, **Compression Mode**).
- The Watcher tab hosts the progress view and the detailed console output; the old "Testing Grounds" tab was repurposed into the Compression Database page.

### Performance

- The fast file-count pass now runs **in parallel** with the size walk, so the determinate progress bar appears almost immediately without waiting for a sequential counting phase.

## v1.0.0 (2026-08-16)

Relaunch of the project as **compactWindows**, a continuation of CompactGUI.

### Modernization & performance

- The folder scan now runs on a background thread with live file/folder counts and cancellation — the UI no longer freezes on large folders.
- The folder is enumerated once (size + file count + directory count) instead of five separate times.
- `compact.exe` is invoked directly as a cancellable background task instead of a persistent CMD shell (removes the `chcp` detection and the `taskkill` hack).
- Console output is buffered through a bounded queue instead of O(n²) `ListBox` inserts.
- The wiki / compression-estimate database is downloaded asynchronously with `HttpClient` (with a timeout) and cached per session.

### Fixes

- Fixed the wiki download URL (it pointed at the old `ImminentFate/CompactGUI` repository).
- Fixed dead links: the `goo.gl` wiki submission form and the About-dialog repo links.
- Guarded `Process.Kill()` when returning to the input page.
- `DirectorySize` and `GetMessageFromModule` now return values on every code path (compiler warnings removed).
- The displayed version now comes from the assembly version instead of a hardcoded string.

### Cleanup

- Removed the unused `Microsoft.Toolkit.Uwp.Notifications` and `System.Management` references.
- Deleted 70 legacy tags and published a clean `v1.0.0` release.
