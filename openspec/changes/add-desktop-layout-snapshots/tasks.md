## 1. Snapshot Foundation and Persistence

- [ ] 1.1 Extend the shared eligible-window snapshot metadata with reusable process path, process start time, Win32 class name, and AppUserModelID lookup where available; verify unavailable process metadata does not omit a normal eligible window.
- [ ] 1.2 Add snapshot, saved desktop, saved window, preview-item, and result-item models with a versioned JSON document repository in the existing local application data location; verify a create/list/load/update/rename/delete round trip preserves timestamps, counts, and records.
- [ ] 1.3 Implement atomic snapshot document writes and defensive load validation; verify a malformed or unsupported document surfaces an error without replacing it or changing windows/desktops.
- [ ] 1.4 Build a capture service that uses the shared eligible-window enumeration and current desktop GUID/order mapping to create a grouped capture preview and persisted snapshot; verify shell, tool, self-management, unassigned, and non-movable windows are excluded.

## 2. Virtual Desktop Resolution and Matching

- [ ] 2.1 Extend the virtual desktop abstraction and every supported Windows implementation with a create-desktop operation; verify the selected implementation can create one desktop and refreshes its ordered desktop identity view.
- [ ] 2.2 Implement restore target resolution that prefers a surviving saved GUID, falls back to saved order, creates only a missing desktop suffix, and preserves extra current desktops; verify GUID fallback, insufficient desktop count, and surplus desktop scenarios.
- [ ] 2.3 Implement a deterministic conservative matcher that scores stable app/window identity fields, reserves unique current-window matches, and classifies each saved record as movable, already correct, not found, or ambiguous; verify duplicate titles and tied candidates are skipped rather than moved.
- [ ] 2.4 Implement the restore executor to revalidate each confirmed window and target, move only movable items through the existing window-desktop mover, continue after recoverable errors, and emit per-window results; verify already-correct and snapshot-external windows remain unchanged.

## 3. Snapshot Management and Restore UI

- [ ] 3.1 Add a reusable desktop layout snapshot management form with snapshot list columns, new, restore, update, inspect, rename, and delete actions; verify the list reflects repository changes and delete affects only the snapshot record.
- [ ] 3.2 Add create and update capture preview dialogs with windows grouped by desktop, detected counts, a timestamped default name, non-empty name validation, and explicit confirmation; verify update saves current layout without moving any window.
- [ ] 3.3 Add restore preview confirmation, non-blocking progress, completion summary, and detail views for moved, already-correct, not-found, ambiguous, and failed results; verify no desktop or window changes occur until confirmation and skipped entries retain their reason.
- [ ] 3.4 Add a `Desktop Layout Snapshots` tray submenu with `New Snapshot`, `Restore Most Recent`, and `Manage Snapshots` commands routed through `App`; verify it is available from every existing tray context menu without changing existing overview or desktop navigation entries.

## 4. Documentation and Verification

- [ ] 4.1 Update the user documentation with snapshot creation, restore preview/result meanings, desktop creation behavior, and safety limits; verify command labels and behavior match the delivered UI.
- [ ] 4.2 Build the solution in Debug configuration and resolve compilation warnings or errors introduced by the feature; verify the build succeeds.
- [ ] 4.3 Validate a multi-desktop session with temporary windows: create, rename, update, inspect, and delete a snapshot; then restore a deliberately misplaced uniquely matched window, a correct window, a closed window, and ambiguous candidates. Verify only the uniquely matched misplaced window moves, missing desktops are added when needed, extra desktops remain, and no application is started or closed.
