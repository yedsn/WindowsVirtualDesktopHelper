## Purpose

Let users preserve working windows and safely dismiss unprotected temporary windows across virtual desktops through the built-in window manager.

## ADDED Requirements

### Requirement: Window manager displays persistent cleanup protection state
The built-in window manager SHALL display a lock-state icon beside every eligible listed application window. The lock state SHALL mean that the window is protected from the application's one-click cleanup action, and SHALL not prevent normal window use, manual closing, desktop movement, or application exit.

#### Scenario: Inspect an unlocked window
- **WHEN** an eligible application window has not been protected for cleanup
- **THEN** the window manager displays it with an unlocked cleanup-protection state

#### Scenario: Toggle a window's cleanup protection
- **WHEN** the user toggles the lock state for an eligible listed window
- **THEN** the window manager changes that window between protected and unprotected cleanup states and updates its displayed icon

#### Scenario: Restart restores a matching lock
- **GIVEN** the user locked an eligible window with application name and window title
- **WHEN** the helper restarts while a window with the same application name and window title remains open
- **THEN** the window manager displays that window as protected

#### Scenario: Lock does not protect another window from the same application
- **GIVEN** the user locked an eligible window with a specific application name and window title
- **WHEN** another eligible window from that application has a different title
- **THEN** the other window is displayed as unprotected

### Requirement: Users can lock every eligible window across desktops
The built-in window manager SHALL provide a `Lock All` action, localized as `全部锁定` in Chinese, that protects every eligible window with ownership resolved to an ordinary virtual desktop when the action is invoked. Invoking the action again SHALL retain existing protection and add protection for any newly eligible, currently unprotected windows on any desktop.

#### Scenario: Lock existing working windows across desktops
- **GIVEN** eligible application windows exist on multiple virtual desktops
- **WHEN** the user invokes `Lock All`
- **THEN** every eligible window with resolved desktop ownership is marked protected in the window manager

#### Scenario: Lock All excludes unresolved and all-desktop windows
- **GIVEN** eligible windows with unresolved ownership or shown on all desktops exist
- **WHEN** the user invokes `Lock All`
- **THEN** those windows are not marked protected

#### Scenario: Lock All includes a newly opened protected window candidate
- **GIVEN** the user previously invoked `Lock All`
- **AND** an additional eligible window was opened on any virtual desktop
- **WHEN** the user invokes `Lock All` again
- **THEN** the previously protected windows remain protected and the additional window becomes protected

### Requirement: One-click cleanup closes only unprotected windows across ordinary desktops
The built-in window manager SHALL provide a `One-click Cleanup` action, localized as `一键清理` in Chinese. Its button label SHALL display the current snapshot's count of eligible unprotected windows across ordinary virtual desktops, using red text when the count is greater than zero. Before the action sends any close request, it SHALL display a confirmation that lists every target and its count. After confirmation, it SHALL take a fresh window view and send a normal close request only to eligible, unprotected windows whose ownership resolves to an ordinary virtual desktop.

#### Scenario: Inspect cleanup target count
- **GIVEN** ordinary virtual desktops have eligible unprotected windows in the window overview snapshot
- **WHEN** the user views the `One-click Cleanup` action
- **THEN** the button label shows those windows and uses red text when the count is greater than zero

#### Scenario: Review cleanup target list before closing
- **GIVEN** ordinary virtual desktops have eligible unprotected windows
- **WHEN** the user invokes `One-click Cleanup`
- **THEN** the window manager lists every target's application name and window title in a confirmation dialog
- **AND** it sends no close request until the user confirms that dialog

#### Scenario: Clean temporary windows after locking the working set
- **GIVEN** ordinary virtual desktops contain protected working windows and unprotected temporary windows
- **WHEN** the user confirms `One-click Cleanup`
- **THEN** the application sends normal close requests to the unprotected temporary windows only
- **AND** it does not send close requests to the protected working windows

#### Scenario: Cancel cleanup confirmation
- **GIVEN** ordinary virtual desktops contain eligible unprotected windows
- **WHEN** the user declines the `One-click Cleanup` confirmation
- **THEN** the application does not send a close request to any window

#### Scenario: Cleanup includes another desktop
- **GIVEN** eligible unprotected windows exist on multiple ordinary virtual desktops
- **WHEN** the user confirms `One-click Cleanup`
- **THEN** the application targets the eligible unprotected windows on every ordinary virtual desktop

#### Scenario: Cleanup has no eligible unprotected target
- **GIVEN** every eligible window on ordinary virtual desktops is protected or no eligible window exists
- **WHEN** the user invokes `One-click Cleanup`
- **THEN** the application does not display a destructive confirmation and reports that there are no unprotected windows to clean

### Requirement: Cleanup protection persists and preserves application safeguards
Cleanup protection SHALL be saved as an exact, case-insensitive application-name and window-title rule, and SHALL be restored after the helper application restarts. The one-click cleanup action SHALL use normal close requests and SHALL not terminate processes, bypass application save prompts, or target windows whose current virtual-desktop ownership cannot be resolved.

#### Scenario: Application asks to save during cleanup
- **GIVEN** an unprotected target application has unsaved work
- **WHEN** the user confirms `One-click Cleanup`
- **THEN** the application receives its normal close request and remains responsible for displaying or handling any save prompt

#### Scenario: Restart preserves cleanup protection
- **GIVEN** the user protected one or more windows
- **WHEN** the helper application is restarted and the window manager is opened again
- **THEN** each still-open window whose application name and title match a saved lock rule is displayed as protected

#### Scenario: Other windows are excluded during cleanup
- **GIVEN** an eligible listed window has unresolved virtual-desktop ownership
- **WHEN** the user confirms `One-click Cleanup`
- **THEN** the application does not target that unresolved window for cleanup

### Requirement: Cleanup reports best-effort outcomes
After a confirmed one-click cleanup action, the window manager SHALL report how many normal close requests were sent and how many target windows could not be requested to close. It SHALL remain usable and provide a refreshed window view after the action.

#### Scenario: A target window disappears before cleanup
- **GIVEN** a window was included in cleanup confirmation
- **WHEN** that window no longer exists when cleanup is performed
- **THEN** the application continues attempting the remaining targets and reports the unavailable window as not closed
