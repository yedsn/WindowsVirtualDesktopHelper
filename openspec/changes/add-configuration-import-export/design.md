## Context

See [proposal.md](proposal.md) for motivation and the `configuration-backup` specification for required behavior. The .NET Framework 4.7.2 WinForms application currently persists settings as a custom text `.config` file below `%AppData%\WindowsVirtualDesktopHelper` and stores all desktop layout snapshots in one versioned JSON file in the same directory. The snapshot manager already owns the user workflow for snapshot lifecycle operations. Virtual desktop order and names are read from the current user's Windows registry; existing code can create desktops but does not expose a common operation for assigning a desktop name.

## Goals / Non-Goals

**Goals:**
- Create one portable, versioned backup document that contains only data the application owns or can safely restore by desktop position.
- Validate the complete backup before changing persisted settings or snapshot data.
- Preserve the existing conservative snapshot restore workflow: import brings snapshots back, but never performs a window-layout restore itself.
- Restore desktop names and a minimum desktop count without deleting or altering local desktops outside the backup's range.
- Keep file persistence recoverable through temporary-file writes and explicit rollback of application-owned data if a write fails.

**Non-Goals:**
- Clone Windows virtual desktop GUIDs, current desktop selection, task-view state, or registry values unrelated to desktop names.
- Move, launch, close, or identify application windows during backup import.
- Merge individual settings or snapshots interactively; import is a whole-backup replacement for application-owned data.
- Provide encryption, cloud synchronization, scheduled backups, or command-line backup operations in this change.

## Decisions

### Use a dedicated versioned JSON backup document instead of copying local storage files

The backup document will include format version, export timestamp, application version, persisted settings as typed key/value records, ordered desktop descriptors containing names only, and the full snapshot document payload. The public format is independent from the `.config` filename and snapshot-file location so it remains portable between installation methods and later storage migrations.

The export will deliberately omit launch arguments because they are process-specific and override saved configuration only at startup. It will omit snapshot `ProcessId` and `WindowHandle` diagnostics because they are transient, even though older snapshot documents retain them locally for troubleshooting. It will not export virtual desktop GUIDs because they are installation-specific.

Copying the local `.config` and snapshot JSON files directly was rejected because the `.config` file includes commented defaults, its name depends on the executable, and it cannot represent desktop names. Backing up the Windows virtual-desktop registry key was rejected because it would carry non-portable identifiers and could disrupt Windows-owned desktop state.

### Expose a controlled settings snapshot and replacement API

The settings component will expose a serializable collection of persisted settings and a validated replacement operation. The export path reads only explicit user configuration, excluding defaults and launch argument overrides. Import parses all entries using the same supported setting value types as the existing settings file, stages validation before writing, and then atomically replaces the primary application config file.

Replacing the internal dictionary from the UI or replaying control events was rejected because it would mix persistence details with forms and would not reliably remove local keys absent from the backup. Rewriting all defaults into the imported file was rejected because it would turn future default changes into sticky user overrides.

### Reuse snapshot validation and add document-level import/export helpers

The snapshot repository will provide validated document serialization and replacement operations, so backup import uses the same schema validation as normal snapshot loading. The complete imported collection replaces the local collection only after every snapshot is normalized and validated. Export captures a stable in-memory snapshot of the current collection.

The backup reader will reject unsupported versions, missing required collections, duplicate or blank setting keys, unsupported value shapes, and invalid snapshot records before it offers confirmation. Unknown fields may be ignored by the serializer only when they do not affect required data; unsupported format versions are rejected rather than guessed.

Directly serializing form-owned snapshot instances was rejected because they can be mid-edit. Treating a backup as a list of independent snapshot imports was rejected because the user asked for a restorable backup rather than a merge workflow.

### Restore desktop configuration by positional names after application data is persisted

The import service will persist validated settings and snapshots as one application-data transaction first, then apply the Windows desktop portion. It will create the missing suffix using the existing desktop-creation abstraction, retrieve fresh desktop IDs, and assign each backed-up name to the corresponding current desktop position. It will preserve every desktop after the last backed-up index. The application must add one version-safe desktop naming operation to its selected virtual desktop abstraction or registry helper.

Windows can reject desktop creation or naming independently of file persistence. For that reason, the operation reports detailed desktop failures without rolling back otherwise valid application-owned backup data. This allows a user to retain restored snapshots/settings and retry desktop configuration rather than losing the whole import to an OS-level transient failure. Desktop names are applied through the existing virtual-desktop registry helper because the application already uses that Windows-owned source to enumerate current desktop IDs and names; using it avoids coupling import support to unstable per-build private COM contracts.

Deleting excess desktops or assigning names by saved GUID was rejected: the former destroys local work and the latter fails across computers. Reusing one version-specific private COM name setter was rejected because virtual desktop COM contracts vary across Windows builds while the current registry helper has a stable, shared positional mapping.

### Place export/import in Settings with preview and confirmation

The Settings form will receive clearly visible `Export Backup` and `Import Backup` actions in a dedicated bottom row. The form's fixed client and minimum heights will include space for that row, and vertical scrolling will keep the row reachable if Windows constrains the window on a small or high-DPI display. Standard save/open file dialogs choose the path. Import reads and validates the file first, then a modal confirmation shows export time and counts for settings, desktops, and snapshots. Completion refreshes the snapshot list, tray UI, hotkey registrations, and settings form values where safe.

Duplicating the dialog flow in each form was rejected because confirmation text, error handling, and post-import refresh requirements must remain consistent. An immediate import after file selection was rejected because settings and snapshot replacement are destructive to local application data.

## Risks / Trade-offs

- [A backup originates from a newer format] -> Reject unsupported versions before confirmation and retain local data unchanged.
- [Writing two application-owned files fails between writes] -> Write staged temporary files first and retain copies of prior files until both replacements succeed; restore the original file if the second replacement fails.
- [A setting value is valid text but no longer accepted by current runtime code] -> Preserve the typed parsing contract and catch runtime refresh errors, report restart requirement or import error without corrupting the saved file.
- [Windows APIs differ across supported builds] -> Add desktop naming through the existing per-version abstraction and report only the affected desktop operation when Windows rejects it.
- [Imported hotkeys conflict with other applications] -> Re-register through the existing tolerant hotkey setup path, which logs per-hotkey registration failures while keeping the rest of the app usable.
- [Imported settings change startup registration] -> Persist the setting but do not automatically add/remove the Windows startup registry entry during import; report that restart or manual Settings confirmation is required to reconcile this external side effect.

## Migration Plan

1. Introduce the backup document format without altering existing `.config` or snapshot file formats.
2. Add the Settings export/import commands, retain the snapshot-manager commands, and make export available even when no snapshots exist.
3. On import, retain original application files until the settings and snapshots replacement succeeds, then apply best-effort desktop naming/count restoration.
4. On rollback, remove the UI entry points and backup service; existing configuration and snapshot files remain in their prior locations and retain their current behavior.
