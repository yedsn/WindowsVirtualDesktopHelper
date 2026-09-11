## Purpose

Provide a portable, validated backup that lets users preserve and restore the application's configuration, virtual desktop naming layout, and saved desktop layout snapshots.

## ADDED Requirements

### Requirement: Users can export a complete portable configuration backup
The application SHALL allow a user to choose a destination and export one versioned backup file. The backup SHALL include all persisted application settings and custom hotkeys, the ordered names of the current virtual desktops, and every saved desktop layout snapshot including its saved window matching rules. The backup SHALL identify its format version and export time. It SHALL not include transient window handles, process IDs, running applications, or any Windows virtual desktop identifier that is not portable across devices.

#### Scenario: Export a populated configuration
- **GIVEN** the user has saved settings, named virtual desktops, and one or more desktop layout snapshots
- **WHEN** the user chooses a backup destination and confirms export
- **THEN** the application writes one backup file containing the settings, ordered desktop names, and all snapshots with their matching rules

#### Scenario: Export when no snapshots exist
- **GIVEN** the user has no saved desktop layout snapshots
- **WHEN** the user confirms export
- **THEN** the application writes a valid backup file with an empty snapshot collection

#### Scenario: Export cannot write the selected destination
- **WHEN** the selected backup destination cannot be created or written
- **THEN** the application reports the failure and does not modify the user's existing settings, desktop configuration, or snapshots

### Requirement: Settings exposes configuration backup actions without clipping controls
The Settings window SHALL provide `Export Backup` and `Import Backup` commands for the complete configuration backup. The window's fixed and minimum dimensions SHALL leave both commands fully visible and accessible, with vertical scrolling available when Windows constrains the window on a small or high-DPI display.

#### Scenario: Settings backup actions are visible
- **WHEN** the user opens the Settings window at its default or minimum size
- **THEN** both backup commands are displayed without being covered or clipped by the window boundary

### Requirement: Users preview and confirm a valid configuration backup before import
The application SHALL allow a user to select a backup file for import. Before changing local data, it SHALL validate that the file is a supported backup format and display a summary containing the saved desktop count, setting count, and snapshot count. The application SHALL require explicit confirmation before performing the import.

#### Scenario: Preview a valid backup
- **WHEN** the user selects a valid supported backup file
- **THEN** the application displays its export time and the counts of settings, desktops, and snapshots before asking for confirmation

#### Scenario: Reject an invalid or unsupported backup
- **WHEN** the user selects a malformed backup file or a file with an unsupported backup version
- **THEN** the application reports that the backup cannot be imported and does not change local settings, virtual desktops, or snapshots

#### Scenario: Cancel an import preview
- **GIVEN** a valid backup summary is displayed
- **WHEN** the user cancels instead of confirming
- **THEN** the application does not change local settings, virtual desktops, or snapshots

### Requirement: Import restores saved application settings and snapshots as a complete backup
After the user confirms import, the application SHALL replace its persisted application settings and complete saved snapshot collection with the validated backup contents. Imported snapshots SHALL preserve their names, timestamps, saved desktop assignments, and window matching rules. The application SHALL refresh active user-interface configuration and shortcut registrations that can be safely refreshed while running, and SHALL clearly notify the user if any imported setting requires restarting the application to take effect.

#### Scenario: Import replaces existing application data
- **GIVEN** local settings and snapshots differ from the selected valid backup
- **WHEN** the user confirms import
- **THEN** the local persisted settings and snapshot collection match the backup, rather than combining old and imported snapshots

#### Scenario: Imported snapshot is available for restoration
- **GIVEN** a valid backup contains a named desktop layout snapshot with edited window rules
- **WHEN** the user confirms import
- **THEN** the snapshot manager lists that snapshot and retains its edited matching rules for a subsequent restore preview

#### Scenario: Imported settings refresh active behavior
- **GIVEN** a valid backup contains a different tray display mode and custom hotkey configuration
- **WHEN** the user confirms import
- **THEN** the application refreshes the tray display and registered hotkeys to reflect the imported settings or informs the user that a restart is required

### Requirement: Import restores backed-up virtual desktop names without disrupting extra local desktops
After the user confirms import, the application SHALL ensure that at least the backup's virtual desktop count exists. It SHALL restore each backed-up desktop name to the local desktop at the same zero-based order. It SHALL not delete, reorder, rename, or otherwise alter local desktops beyond the backed-up desktop range, and it SHALL not move or close application windows as part of importing configuration.

#### Scenario: Import creates missing desktops and restores names
- **GIVEN** a backup records three named desktops and the local system has one desktop
- **WHEN** the user confirms import
- **THEN** the application creates enough desktops to reach three and assigns the backed-up names to desktop positions one through three

#### Scenario: Import preserves extra local desktops
- **GIVEN** a backup records two named desktops and the local system has four desktops
- **WHEN** the user confirms import
- **THEN** the application restores names for the first two desktops and leaves the third and fourth desktops unchanged

#### Scenario: Import does not restore running window placement
- **GIVEN** currently open windows are arranged differently from the backup's snapshots
- **WHEN** the user confirms import
- **THEN** the import itself does not move, start, close, or otherwise alter those windows

### Requirement: Import failures do not leave partially imported application data
The application SHALL validate all importable application data before writing it. If persistence of settings or snapshots fails during a confirmed import, the application SHALL report the failure and preserve or restore the prior application data so it is not left partially imported. Failures while creating or naming virtual desktops SHALL be reported clearly and SHALL not prevent the already validated backup data from remaining available for a later import attempt.

#### Scenario: Settings or snapshot persistence fails
- **GIVEN** a validated backup was confirmed
- **WHEN** the application cannot persist imported settings or snapshots
- **THEN** it reports the failure and leaves the prior persisted application settings and snapshots intact

#### Scenario: Desktop restoration is only partially accepted by Windows
- **GIVEN** the backup data has been persisted successfully
- **WHEN** Windows rejects creation or naming of one or more required desktops
- **THEN** the application reports which desktop configuration could not be restored and retains the imported settings and snapshots
