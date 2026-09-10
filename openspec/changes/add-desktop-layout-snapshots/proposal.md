## Why

Users who organize active applications across Windows virtual desktops can accidentally disrupt that arrangement, then have to manually locate and move each window back. The application already understands virtual desktop ownership and can move eligible windows, so it can provide a safe, local snapshot workflow that restores only confidently identified, already-open windows.

## What Changes

- Add a desktop layout snapshot manager accessible from the application and tray menu, including create, list, inspect, rename, update, delete, and restore operations.
- Persist named local snapshots containing the ordered virtual desktop layout and durable window-identification metadata for eligible top-level application windows.
- Add restore analysis and confirmation that classify every saved window as movable, already correctly placed, not found, or ambiguous before any move occurs.
- Restore only uniquely matched open windows, creating missing target desktops when required; retain extra current desktops and never start, close, or otherwise modify applications.
- Show per-window restore progress and a detailed outcome, including skipped or failed items and their reasons.

## Capabilities

### New Capabilities
- `desktop-layout-snapshots`: Provides local desktop-layout snapshot lifecycle management and conservative restoration of already-open application windows to virtual desktops.

### Modified Capabilities
- None.

## Impact

- Affects the WinForms UI, shared tray context menu, and the application coordination layer.
- Adds local JSON snapshot persistence, snapshot/window matching, restore-result models, and dialogs for management, preview, progress, and results.
- Extends the internal virtual-desktop abstraction where needed to enumerate ordered desktop identities, create a desktop, and move windows to a resolved target desktop.
- Builds on the existing eligible-window enumerator and documented Windows virtual desktop manager; adds no external dependency and does not alter existing window overview, task-view, or desktop-switching behavior.
