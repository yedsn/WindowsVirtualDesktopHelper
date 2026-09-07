## Why

The tray currently exposes only the current desktop number and optional previous/next controls. Users with several virtual desktops must open the tray menu or navigate step by step to reach a specific desktop, which adds friction to a frequent action.

## What Changes

- Add a persisted tray desktop display mode with two mutually exclusive choices: navigation controls and all desktop numbers.
- Keep navigation controls as the default mode, preserving the current previous, current-number, and next tray experience.
- Add an all-desktops mode that displays one numbered tray icon per virtual desktop; left-clicking an icon switches directly to its desktop.
- Synchronize the all-desktops icon set when the virtual desktop count, current desktop, selected display mode, or system theme changes.
- Update the Settings window so mode-specific controls are shown only for the active tray display mode.

## Capabilities

### New Capabilities
- `tray-desktop-display-modes`: Select and operate either navigation-style tray controls or direct per-desktop tray icons.

### Modified Capabilities
- None.

## Impact

- Affects tray icon ownership, creation, disposal, click handling, and refresh logic in `Source/App/App.cs` and `Source/Forms/AppForm.*`.
- Adds a persisted setting and Settings form controls in `Source/App/Settings.cs` and `Source/Forms/SettingsForm.*`.
- Updates user-facing settings documentation in `Documentation/Settings.md`.
- Reuses the existing virtual-desktop API's direct-switch operation and notification-icon renderer; no external API or dependency changes are expected.
