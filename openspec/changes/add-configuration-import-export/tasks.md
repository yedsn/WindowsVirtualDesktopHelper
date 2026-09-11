## 1. Backup Data and Persistence

- [x] 1.1 Add versioned backup document models for persisted settings, ordered desktop names, and complete snapshot data; verify valid documents serialize and unsupported or incomplete documents are rejected before import confirmation.
- [x] 1.2 Expose typed persisted-settings export, staged validation, and atomic replacement APIs that exclude defaults and launch arguments; verify a settings export/import round trip retains custom values and removes local keys absent from the imported backup.
- [x] 1.3 Extend the snapshot repository with validated backup serialization and full-collection replacement; verify exported snapshots preserve timestamps and edited matching rules, while malformed snapshot data leaves the local file unchanged.
- [x] 1.4 Implement a backup service that exports to a user-selected file and validates an import file into an immutable summary; verify successful export with zero snapshots and failed reads/writes do not alter local application data.
- [x] 1.5 Implement staged replacement and rollback for settings and snapshots; verify an injected failure during either persistence step leaves both prior application-owned data sets intact.

## 2. Virtual Desktop Configuration Restore

- [x] 2.1 Add a version-compatible operation to assign a name to a current virtual desktop by index, using the existing virtual desktop abstraction; verify it can rename an existing desktop and reports a supported error when Windows rejects the operation.
- [x] 2.2 Implement import-time desktop restoration that creates only a missing suffix and applies backed-up names by order; verify a three-desktop backup imports into a one-desktop system and a two-desktop backup preserves all local desktops after index one.
- [x] 2.3 Report partial desktop creation or naming failures without moving windows or rolling back already persisted backup data; verify a simulated desktop failure produces a clear outcome while imported settings and snapshots remain available.

## 3. Application and User Interface Integration

- [x] 3.1 Add `Export Backup` and `Import Backup` commands to the Settings form without requiring snapshot selection or a saved snapshot.
- [x] 3.2 Implement export save-file selection, successful completion feedback, and actionable write-error feedback; verify the selected backup file is created and contains expected settings, desktop names, and snapshots.
- [x] 3.3 Implement import file selection, pre-change validation summary, cancellation, and explicit confirmation; verify invalid and cancelled imports change no local settings, snapshots, desktops, or open windows.
- [x] 3.4 Connect confirmed import to settings/snapshot replacement, desktop restoration, snapshot-list refresh, tray refresh, hotkey re-registration, and settings-form reload; verify imported snapshots appear immediately and active tray/hotkey behavior reflects import where supported.
- [x] 3.5 Surface restart or external-side-effect notices for imported settings that cannot safely take effect live, including startup registration; verify the user receives a clear completion summary for data and desktop restoration outcomes.
- [x] 3.6 Make the Settings form the only backup entry point and increase the fixed/minimum Settings height so both commands remain visible.

## 4. Documentation and Verification

- [x] 4.1 Document backup contents, export/import steps, desktop-name and count restoration rules, preservation of extra desktops, and limits that windows are not moved by import; verify labels match the delivered UI.
- [x] 4.2 Build the solution in Debug configuration and resolve compilation warnings or errors introduced by the feature; verify the build succeeds.
- [x] 4.3 Manually validate a populated backup round trip: export settings, named desktops, and edited snapshots; change local data; import; confirm settings and snapshots are replaced, missing desktops/names are restored, extra desktops remain unchanged, and no window moves during import.
