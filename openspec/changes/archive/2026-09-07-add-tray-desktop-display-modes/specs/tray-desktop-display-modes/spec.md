## Purpose

Provide selectable notification-area desktop controls so users can either navigate between adjacent desktops or jump directly to any currently available virtual desktop.

## ADDED Requirements

### Requirement: Persisted tray desktop display mode

The application SHALL provide a persisted tray desktop display mode with the values `navigation` and `all-desktops`. The default value SHALL be `navigation` so existing installations retain their current tray layout when no mode is configured.

#### Scenario: Existing configuration is upgraded
- **GIVEN** a user configuration does not contain a tray desktop display mode
- **WHEN** the application starts after upgrading
- **THEN** the application SHALL use the `navigation` mode

#### Scenario: User changes the display mode
- **GIVEN** the Settings window is open
- **WHEN** the user selects a different tray desktop display mode
- **THEN** the application SHALL apply the selected mode immediately
- **AND** persist the selected mode when settings are saved

### Requirement: Navigation tray display

When the tray desktop display mode is `navigation`, the application SHALL display the current desktop number and retain the existing previous and next desktop tray controls and their configured behavior.

#### Scenario: Navigation mode is selected
- **GIVEN** the application manages one or more virtual desktops
- **WHEN** the selected tray desktop display mode is `navigation`
- **THEN** the tray SHALL show the current desktop number
- **AND** the existing previous and next desktop controls SHALL be governed by their existing configuration

### Requirement: All-desktops tray display

When the tray desktop display mode is `all-desktops`, the application SHALL display exactly one numbered tray icon for each currently available virtual desktop. The icons SHALL be numbered consecutively from 1 through the current virtual desktop count.

#### Scenario: Display all desktop numbers
- **GIVEN** there are four virtual desktops
- **WHEN** the selected tray desktop display mode is `all-desktops`
- **THEN** the tray SHALL display icons numbered 1, 2, 3, and 4
- **AND** it SHALL not display the previous or next desktop tray controls

#### Scenario: Jump directly from a numbered icon
- **GIVEN** the selected tray desktop display mode is `all-desktops`
- **AND** a tray icon represents desktop 3
- **WHEN** the user left-clicks that icon
- **THEN** the application SHALL switch to desktop 3

#### Scenario: Context actions remain available
- **GIVEN** the selected tray desktop display mode is `all-desktops`
- **WHEN** the user opens the context menu of a numbered desktop icon
- **THEN** the application SHALL provide its existing application context actions

### Requirement: Tray display synchronization

The application SHALL synchronize its tray desktop display with the selected mode, the current virtual desktop count, and the current theme. It SHALL remove icons that no longer belong to the active display mode or to an existing virtual desktop.

#### Scenario: Desktop count increases in all-desktops mode
- **GIVEN** the selected tray desktop display mode is `all-desktops`
- **AND** the tray displays icons for desktops 1 through 3
- **WHEN** the virtual desktop count changes to 4
- **THEN** the tray SHALL display an additional icon numbered 4 without requiring an application restart

#### Scenario: Desktop count decreases in all-desktops mode
- **GIVEN** the selected tray desktop display mode is `all-desktops`
- **AND** the tray displays icons for desktops 1 through 4
- **WHEN** the virtual desktop count changes to 3
- **THEN** the tray SHALL remove the icon for desktop 4 without requiring an application restart

#### Scenario: User switches mode
- **GIVEN** the tray displays the `navigation` mode controls
- **WHEN** the user selects `all-desktops` mode
- **THEN** the tray SHALL remove the navigation controls
- **AND** display one numbered icon for each available desktop

#### Scenario: Application exits
- **GIVEN** the application has displayed tray icons for one of the modes
- **WHEN** the application exits
- **THEN** it SHALL remove all tray icons that it created

### Requirement: Mode-relevant settings UI

The Settings window SHALL present the two tray desktop display modes as mutually exclusive choices. It SHALL not present previous/next tray-control settings while `all-desktops` is selected, because those controls are not displayed in that mode.

#### Scenario: All-desktops mode hides navigation-only settings
- **GIVEN** the Settings window is open
- **WHEN** the user selects `all-desktops` mode
- **THEN** the Settings window SHALL hide the previous/next tray-control setting

#### Scenario: Navigation mode shows navigation-only settings
- **GIVEN** the Settings window is open
- **WHEN** the user selects `navigation` mode
- **THEN** the Settings window SHALL show the previous/next tray-control setting
