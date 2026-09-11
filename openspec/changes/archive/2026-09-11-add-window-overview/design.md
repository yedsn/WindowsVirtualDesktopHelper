## Context

The existing tray `ContextMenuStrip` dynamically adds desktop-switching entries. `Util.OS` already contains small user32 wrappers, while `IVirtualDesktopManager` exposes only current-desktop information and switching. Two inactive Windows 11 API implementations contain ad hoc title-only `EnumWindows` helpers, but the selected implementation is version dependent and those helpers do not provide a reliable, shared ownership model.

See [proposal.md](proposal.md) for the motivation and [window overview specification](specs/window-overview/spec.md) for user-visible behavior.

## Goals / Non-Goals

**Goals:**
- Use a shared top-level-window snapshot that is independent of the active private virtual-desktop implementation.
- Associate a snapshot window with a numbered virtual desktop when the operating system can provide an ownership ID.
- Keep the overview responsive and useful when an individual window, Explorer, or a desktop ownership lookup fails.
- Perform activation and close operations only against a validated window handle from the latest snapshot.

**Non-Goals:**
- No continuous monitoring, notification, process management, window moving, desktop creation, or batch close.
- No guarantee of controlling elevated, protected, or other-session windows.
- No attempt to reproduce Task View thumbnails or expose every Win32 window.

## Decisions

### Create a shared window snapshot model and user32 enumerator

Add a small immutable window-record model containing the HWND, PID, process display name, title, and desktop ownership result. Add a shared `EnumWindows`-based utility under `Source/Util` that gets these fields, filters non-user-facing top-level windows, and supplies activation and `WM_CLOSE` operations.

This centralizes the behavior rather than extending the two legacy `GetWindows()` implementations. `Process.GetProcesses()` was rejected because it loses individual windows and virtual desktop assignment. Window-title-only enumeration was rejected because it makes duplicate/blank titles and cleanup actions unsafe.

### Use the system virtual desktop manager for per-window IDs and adapt desktop enumeration behind the existing abstraction

Use the system virtual desktop manager's `GetWindowDesktopId(HWND)` operation to obtain a desktop GUID for an eligible window. Extend the application's internal desktop abstraction so each selected private API implementation can return the ordered desktop IDs it already enumerates for switching. Join the GUID to that ordered list to derive `Desktop 1`, `Desktop 2`, and so on.

The documented per-window manager avoids duplicating a version-specific `GetWindowDesktopId` COM declaration in every private implementation. The project must still update each private implementation for the ordered desktop-ID enumeration because its undocumented desktop COM interfaces vary by Windows release. A failed lookup produces the fallback group, not a failed overview.

### Use a single reusable WinForms dialog rather than a nested tray menu

Add one `WindowOverviewForm` instance managed by `App`. The fixed `All Windows...` tray command shows or focuses it and requests a new snapshot. The dialog uses a search field, desktop-grouped list, refresh control, and explicit activate/close controls.

A long dynamic submenu was rejected: it does not scale, cannot safely expose confirmation, and has poor discoverability for grouping and filtering. A new tray icon was rejected because the feature belongs with application management commands and should work in both tray layout modes.

### Treat actions as best-effort and revalidate HWNDs

Before activation or close, verify the HWND still exists. Activation switches desktop first when an assigned non-current desktop exists, then calls the foreground operation. Closing sends a normal close request after confirmation; it does not terminate the process. Any failed operation results in a user-facing status message and leaves the dialog available for refresh.

Forced process termination was rejected because it bypasses unsaved-work prompts and is inappropriate for a cleanup view.

## Risks / Trade-offs

- [Windows changes the private virtual desktop COM contracts] -> Keep the shared enumerator independent from private APIs and return unknown ownership for unsupported or failed mappings.
- [A window disappears or changes title after the snapshot] -> Revalidate handles at action time and refresh after close attempts.
- [Window filtering includes a shell/tool window or excludes an unusual application] -> Use conservative user-window eligibility checks and keep filtering isolated for targeted refinements.
- [Foreground restrictions prevent activation] -> Switch the target desktop first, attempt normal foregrounding, and report failure without blocking the overview.
- [Cross-thread COM/UI work causes a frozen dialog] -> Build snapshots outside the UI update where practical and marshal only final list updates to the dialog thread.

## Migration Plan

1. Ship the feature as an additional tray command with no configuration migration.
2. Retain all existing Task View and desktop switching behavior.
3. If desktop-ID enumeration fails on a Windows build, retain the dialog and show affected windows in `Other windows`; removing the command is unnecessary for rollback.
