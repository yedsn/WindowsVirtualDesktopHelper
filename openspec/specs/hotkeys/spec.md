# hotkeys Specification

## Purpose
TBD - created by archiving change add-task-view-hotkey-action. Update Purpose after archive.

## Requirements

### Requirement: Task View hotkey action

The application SHALL support a configurable hotkey action named `TaskView` that opens Windows Task View.

The application SHALL include an opt-in built-in setting named `feature.useHotKeyToOpenTaskView` with the default value `false` and a configurable hotkey value named `feature.useHotKeyToOpenTaskView.hotkey` with the default value `Alt + D`.

#### Scenario: User binds Alt+D to Task View

- **GIVEN** the user configures `hotkeys.openTaskView = "Alt + D = TaskView"`
- **WHEN** the application registers hotkeys and the user presses `Alt + D`
- **THEN** the application opens Windows Task View

#### Scenario: User enables built-in Task View hotkey setting

- **GIVEN** the user configures `feature.useHotKeyToOpenTaskView: true`
- **AND** `feature.useHotKeyToOpenTaskView.hotkey` is `Alt + D`
- **WHEN** the application registers hotkeys and the user presses `Alt + D`
- **THEN** the application opens Windows Task View

#### Scenario: User enables Task View hotkey from settings UI

- **GIVEN** the settings window is open
- **WHEN** the user checks the Task View hotkey option
- **THEN** the application stores `feature.useHotKeyToOpenTaskView: true`
- **AND** the application refreshes registered hotkeys

#### Scenario: Task View action uses existing invocation behavior

- **GIVEN** the user triggers the `TaskView` action from a configured hotkey
- **WHEN** the action is executed
- **THEN** the application uses the existing Task View opening behavior

#### Scenario: Existing action validation still applies

- **GIVEN** a configured Task View hotkey is missing a modifier or regular key
- **WHEN** the application parses hotkey settings
- **THEN** the hotkey is rejected using the existing invalid-hotkey logging behavior
