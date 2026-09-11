## Why

Application settings, saved desktop layout snapshots, and virtual desktop names are stored locally and separately. Users need one portable backup they can keep before reinstalling Windows or moving to another computer, then import to restore their working environment without manually reconstructing it.

## What Changes

- Add a versioned backup file format that exports the application's saved settings, custom hotkeys, virtual desktop names and order, and every desktop layout snapshot including its window matching rules.
- Add export and import actions to the Settings window, using standard file dialogs so users choose the backup destination or source.
- Validate a selected backup before changing local data, show a clear import summary and confirmation, and write imported settings and snapshots safely.
- During import, ensure the backup's desktop count exists and restore names by desktop order; do not delete, reorder, or rename local desktops beyond the backed-up range.
- Apply imported settings to the running application where practical, refresh affected UI and hotkeys, and clearly report when a restart is required for settings that cannot safely be reloaded live.

## Capabilities

### New Capabilities
- `configuration-backup`: Export and import a portable, versioned backup of application settings, virtual desktop configuration, and desktop layout snapshots.

## Impact

- Affects the settings persistence API, desktop layout snapshot repository/service, virtual desktop registry abstraction, application coordination layer, and WinForms Settings UI.
- Adds a local backup document model, validation, atomic file writes, import preview/confirmation, and desktop-name restoration behavior.
- Uses existing .NET Framework serialization and WinForms file dialogs; no external dependency or network access is required.
