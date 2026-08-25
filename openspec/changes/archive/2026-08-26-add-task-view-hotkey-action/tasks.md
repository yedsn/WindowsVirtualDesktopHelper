# Tasks

## Implementation

- [x] Add `TaskView` handling to `App.RunAction()`.
- [x] Update the supported-actions comment near `RunAction()`.
- [x] Add opt-in `feature.useHotKeyToOpenTaskView` settings.
- [x] Add a settings UI checkbox for the built-in Task View hotkey.
- [x] Update hotkey documentation with the new `TaskView` action and an `Alt + D` example.
- [x] Update action documentation so the supported action list is no longer a placeholder.

## Verification

- [x] Build the application in Release mode with signing skipped locally.
- [x] Confirm the generated executable is produced.
- [ ] Manually verify `hotkeys.openTaskView = "Alt + D = TaskView"` opens Windows Task View.
- [x] Confirm existing hotkey actions still compile and remain documented.
