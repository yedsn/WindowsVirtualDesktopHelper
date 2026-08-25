# Add Task View Hotkey Action Design

## Current Flow

Custom hotkeys are discovered from settings keys beginning with `hotkeys.`. Each value is parsed as:

```txt
<modifier + key> = <action>
```

The hotkey handler calls `App.RunAction(action)`. `RunAction()` currently supports desktop switching actions such as `DesktopForward`, `DesktopBackward`, `PreviousDesktop`, and `Desktop1` through `Desktop99`.

Task View can already be opened through `Util.OS.OpenTaskView()`, which simulates the Windows `Win + Tab` shortcut.

## Proposed Flow

Add `TaskView` as a supported action:

```txt
hotkeys.openTaskView = "Alt + D = TaskView"
```

Also add a built-in opt-in feature setting:

```txt
feature.useHotKeyToOpenTaskView = false
feature.useHotKeyToOpenTaskView.hotkey = "Alt + D"
```

Runtime flow:

```txt
Alt + D
  -> KeyboardHook event
  -> App._hotKeyPressed()
  -> App.RunAction("TaskView")
  -> Util.OS.OpenTaskView()
  -> Windows Task View opens
```

## Action Name

Use `TaskView` as the primary action name because it is short and matches the Windows feature name.

Optionally support `OpenTaskView` as an alias if we want the action to read like a command. The minimum implementation only needs `TaskView`.

## Files To Change

- `Source/App/App.cs`: add `TaskView` handling in `RunAction()` and update the local supported-actions comment.
- `Source/App/Settings.cs`: add the opt-in Task View hotkey defaults.
- `Source/Forms/AppForm.cs`: reuse the app-level Task View opening method from tray icon clicks.
- `Source/Forms/SettingsForm.cs` and `Source/Forms/SettingsForm.Designer.cs`: add a settings checkbox that toggles the built-in Task View hotkey.
- `Documentation/Hotkeys.md`: add `TaskView` to the hotkey action list and include an example.
- `Documentation/Actions.md`: replace the placeholder with the supported action list, including `TaskView`.
- `Documentation/Settings.md`: document the new Task View hotkey settings.

## Behavior

- The feature is opt-in through custom configuration.
- The settings UI includes a checkbox for the default `Alt + D` Task View hotkey.
- Task View hotkeys and tray icon clicks use the same app-level opening behavior.
- Existing hotkey validation still applies: at least one modifier and one regular key are required.
- Failed hotkey registration continues to be logged without crashing, matching existing behavior.
- If another application or Windows reserves `Alt + D`, registration failure should be handled by the existing registration error path.

## Testing

- Build Release with post-build signing disabled locally.
- Add a config entry such as `hotkeys.openTaskView = "Alt + D = TaskView"` and verify the app registers without errors.
- Press `Alt + D` and verify Windows Task View opens.
- Verify existing actions still work: `DesktopForward`, `DesktopBackward`, `PreviousDesktop`, and `Desktop1`.
