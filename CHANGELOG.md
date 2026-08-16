# Changelog

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
