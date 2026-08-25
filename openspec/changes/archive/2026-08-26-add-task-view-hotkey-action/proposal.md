# Add Task View Hotkey Action

## Summary

Add a configurable hotkey action for opening Windows Task View.

Users should be able to define a custom hotkey such as:

```txt
hotkeys.openTaskView = "Alt + D = TaskView"
```

When the hotkey is pressed, Windows Virtual Desktop Helper should open Task View using the existing Task View invocation path.

## Motivation

The application already supports custom global hotkeys and already has an internal `OpenTaskView()` helper used by tray icon clicks. However, Task View is not currently exposed through the Actions API, so it cannot be bound through the existing `hotkeys.*` configuration mechanism.

Adding this action keeps the feature small and consistent with existing behavior while avoiding a larger settings UI change.

## Scope

In scope:

- Add a new action name for opening Task View.
- Route the new action to the existing `Util.OS.OpenTaskView()` method.
- Document the new action in hotkey/action documentation.
- Verify a Release build succeeds.

Out of scope:

- Adding a new checkbox or hotkey editor to the settings UI.
- Enabling `Alt + D` by default.
- Replacing the current Win + Tab simulation implementation.

## Notes

`Alt + D` is commonly used by browsers and File Explorer to focus the address bar. The feature should therefore remain opt-in through configuration rather than being enabled by default.
