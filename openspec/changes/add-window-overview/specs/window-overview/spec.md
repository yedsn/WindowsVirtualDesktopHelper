## Purpose

Provide a single, actionable overview of eligible application windows across all virtual desktops so users can find and clean up applications without visiting each desktop individually.

## ADDED Requirements

### Requirement: Tray menu opens the window overview
The application SHALL provide an `All Windows...` command in every tray context menu. Selecting the command SHALL open the cross-virtual-desktop window overview.

#### Scenario: Open the overview from the tray
- **WHEN** the user selects `All Windows...` from a tray icon context menu
- **THEN** the application displays the window overview

### Requirement: Overview lists windows across virtual desktops
The window overview SHALL take a fresh snapshot of eligible top-level application windows when it opens and group each identified window by its virtual desktop. Each listed window SHALL show its application identity and window title.

#### Scenario: Windows are grouped by desktop
- **WHEN** eligible windows exist on more than one virtual desktop
- **THEN** the overview displays separate desktop groups containing the windows assigned to each desktop

#### Scenario: System and non-actionable windows are excluded
- **WHEN** the operating system enumerates hidden, shell-host, or non-actionable top-level windows
- **THEN** the overview does not present those windows as application cleanup targets

### Requirement: Overview retains windows with unresolved desktop ownership
The window overview SHALL display an eligible window even when its virtual desktop cannot be determined. Such windows SHALL appear in a distinct `Other windows` group rather than causing the snapshot to fail.

#### Scenario: Desktop ownership query fails for one window
- **WHEN** a window can be enumerated but its virtual desktop ownership cannot be resolved
- **THEN** the overview lists the window under `Other windows` and continues to display other windows

### Requirement: Overview supports filtering and refresh
The window overview SHALL provide a text filter that matches application identity or window title, and a refresh command that replaces the displayed snapshot with the current eligible windows.

#### Scenario: Filter windows by title
- **WHEN** the user enters text that matches a listed window title
- **THEN** the overview displays matching windows and their containing desktop group

#### Scenario: Refresh after a window closes
- **WHEN** a listed application window closes and the user requests refresh
- **THEN** the closed window is absent from the refreshed overview

### Requirement: Overview activates a selected window
The window overview SHALL allow the user to activate a listed window. For a window assigned to another virtual desktop, the application SHALL first switch to that desktop and then attempt to foreground the window.

#### Scenario: Activate a window on another desktop
- **WHEN** the user activates a window assigned to a non-current virtual desktop
- **THEN** the application switches to that virtual desktop and attempts to foreground the selected window

#### Scenario: Window no longer exists during activation
- **WHEN** the user activates a window that has closed since the snapshot was created
- **THEN** the application keeps the overview usable and indicates that the window is no longer available

### Requirement: Overview closes one selected window safely
The window overview SHALL offer a close action for one selected window and SHALL request confirmation before sending that window a close request. The close action SHALL not provide a batch-close operation.

#### Scenario: Confirm closing a selected window
- **WHEN** the user confirms the close action for one listed window
- **THEN** the application requests that selected window close and refreshes the overview

#### Scenario: Cancel closing a selected window
- **WHEN** the user declines the close confirmation
- **THEN** the application leaves the selected window open and keeps the overview displayed
