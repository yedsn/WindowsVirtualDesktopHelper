## Purpose

Provide named local snapshots of eligible open application windows and their virtual desktop assignments, then safely restore that layout without recreating or disrupting applications.

## ADDED Requirements

### Requirement: Users manage named desktop layout snapshots
The application SHALL provide a desktop layout snapshot entry point from the tray menu and a management view listing each saved snapshot's name, creation time, update time, saved desktop count, and saved window count. The application SHALL allow the user to create, inspect, update, rename, and delete a snapshot.

#### Scenario: Open snapshot management from the tray
- **WHEN** the user selects the desktop layout snapshot management command from a tray context menu
- **THEN** the application displays the saved snapshot list and its management actions

#### Scenario: Rename a snapshot
- **GIVEN** a saved snapshot named `Development`
- **WHEN** the user supplies a new valid name and confirms rename
- **THEN** the management view displays the new name and preserves that snapshot's recorded layout

#### Scenario: Delete a snapshot
- **GIVEN** a saved snapshot named `Development`
- **WHEN** the user confirms deletion
- **THEN** the application removes only the saved snapshot record and does not change running applications or current virtual desktops

### Requirement: Creating or updating a snapshot captures an eligible window layout
The application SHALL create or update a named snapshot from the currently eligible top-level application windows and their virtual desktop assignments. Each included window record SHALL retain desktop order and sufficient application and window identity metadata to support a later conservative match. The capture preview SHALL disclose the detected desktop count, included window count, and windows grouped by virtual desktop.

#### Scenario: Create a snapshot from a multi-desktop layout
- **GIVEN** eligible application windows are open on multiple virtual desktops
- **WHEN** the user confirms creation of a named snapshot
- **THEN** the application saves the included windows with their current desktop assignments and displays the saved desktop and window counts

#### Scenario: Update a snapshot without moving windows
- **GIVEN** a saved snapshot exists
- **WHEN** the user confirms an update using the current layout
- **THEN** the application replaces the snapshot's saved layout with the current eligible windows and does not move any window

#### Scenario: Ineligible windows are excluded from capture
- **WHEN** the operating system enumerates hidden, shell-host, application-management, or otherwise non-movable windows
- **THEN** the application excludes them from the snapshot and does not present them as saved application windows

### Requirement: Restore preview classifies saved windows conservatively
Before a restore changes any window placement, the application SHALL analyze every saved window against currently open eligible windows and show a preview grouped into `Can restore`, `Already on target desktop`, `Not found`, and `Ambiguous match`. A saved window SHALL be eligible to move only when exactly one current window matches its stored identity metadata with sufficient confidence.

#### Scenario: Preview identifies a misplaced uniquely matched window
- **GIVEN** a saved window has exactly one current matching window on a different desktop
- **WHEN** the user requests restore preview
- **THEN** the application classifies it as `Can restore` and identifies its current and target desktops

#### Scenario: Preview skips a closed application
- **GIVEN** a saved application window is no longer open
- **WHEN** the user requests restore preview
- **THEN** the application classifies it as `Not found` and does not offer to start the application

#### Scenario: Preview skips non-unique candidates
- **GIVEN** more than one current window is a plausible match for a saved window
- **WHEN** the user requests restore preview
- **THEN** the application classifies it as `Ambiguous match` and excludes all candidates from movement

### Requirement: Restore moves only uniquely matched open windows to resolved target desktops
After the user confirms a restore preview, the application SHALL move only windows classified as `Can restore` to their resolved target desktops. The application SHALL use a current desktop with the saved desktop identity when available, otherwise use the saved desktop order; it SHALL create missing desktops when the current desktop collection is shorter than the snapshot requires. The application SHALL not remove extra current desktops.

#### Scenario: Restore a misplaced saved window
- **GIVEN** a saved window is uniquely matched and is currently on a different desktop
- **WHEN** the user confirms restore
- **THEN** the application moves that window to the resolved target desktop

#### Scenario: Restore creates missing target desktops
- **GIVEN** the snapshot requires three ordered desktops and the current collection has one
- **WHEN** the user confirms restore for a window assigned to the third saved desktop
- **THEN** the application creates the missing desktops and moves the uniquely matched window to the third resolved desktop

#### Scenario: Restore retains extra current desktops
- **GIVEN** the snapshot requires three desktops and the current collection has five
- **WHEN** the user confirms restore
- **THEN** the application retains all five current desktops and only uses resolved targets for the snapshot layout

### Requirement: Restore preserves application and unrelated-window state
The application SHALL never start an application, close an application, terminate a process, modify application content, move a window that is not saved by the selected snapshot, or force movement of an unresolved or ambiguous saved window. It SHALL leave a uniquely matched window already assigned to its resolved target desktop in place.

#### Scenario: Snapshot-external window is not affected
- **GIVEN** an eligible open window is absent from the selected snapshot
- **WHEN** the user confirms restore
- **THEN** the application does not move, close, or otherwise change that window

#### Scenario: Matching window is already on its target desktop
- **GIVEN** a saved window is uniquely matched and already assigned to its resolved target desktop
- **WHEN** the user confirms restore
- **THEN** the application leaves it in place and reports it as already correctly placed

### Requirement: Restore exposes progress and per-window outcomes
The application SHALL show progress while executing a confirmed restore and display a completion summary with counts for moved windows, windows already correctly placed, windows not found, ambiguous matches, and failures. The result detail SHALL identify each saved window and explain its outcome.

#### Scenario: A window move fails
- **GIVEN** a window was classified as restorable but Windows rejects its move
- **WHEN** the application processes that window
- **THEN** the application continues processing the remaining restorable windows and reports that window as failed with a reason

#### Scenario: Restore completes with skipped windows
- **GIVEN** the preview includes not-found or ambiguous saved windows
- **WHEN** the confirmed restore completes
- **THEN** the completion summary and details retain those skipped outcomes rather than reporting them as system failures
