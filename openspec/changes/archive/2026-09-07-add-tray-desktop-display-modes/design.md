## Context

See [proposal.md](proposal.md) for motivation and the accompanying tray-desktop-display-modes specification for behavior. The WinForms application currently owns four designer-created `NotifyIcon` instances: current desktop number, optional desktop name initial, previous desktop, and next desktop. Direct switching already accepts a zero-based desktop index. A polling loop tracks the virtual desktop count but currently does not refresh the tray when that count changes.

The new mode requires a variable number of notification-area icons. The notification icon renderer caches generated `Icon` instances, but each `NotifyIcon` remains a disposable component that must be hidden and disposed when removed.

## Goals / Non-Goals

**Goals:**
- Add a two-value persisted mode without changing the default experience for existing configurations.
- Make the variable desktop icon set reliable across mode changes, desktop additions/removals, theme changes, and application exit.
- Reuse the existing desktop switch operation, context menu, and icon rendering behavior.

**Non-Goals:**
- Adding tray controls for creating, deleting, renaming, or reordering virtual desktops.
- Changing virtual desktop API implementations or the existing right-click desktop menu behavior.
- Adding a distinct visual style for the currently active desktop in all-desktops mode.
- Removing legacy navigation configuration keys; they remain effective only in navigation mode for configuration compatibility.

## Decisions

### Use one enumerated display-mode setting

Add `feature.iconTray.desktopDisplayMode` with `navigation` as its default and `all-desktops` as the alternate value. The Settings UI uses two radio buttons, making the mutually exclusive choice apparent and avoiding ambiguous combinations of feature flags.

The existing `feature.showPrevNextIcons` configuration remains in place and is consulted only by navigation mode. This avoids breaking user-managed configuration files and preserves the current navigation behavior. A new boolean such as `feature.showAllDesktopIcons` was rejected because two independent booleans could enable conflicting layouts.

### Keep dynamic icons in an owned collection

Add an AppForm-owned collection for the numbered `NotifyIcon` objects used only in all-desktops mode. Each icon receives the shared context menu and a click handler bound to its zero-based desktop index. The existing designer-created icons remain responsible for navigation mode.

Recreating this list is preferred over attempting to resize fixed designer fields because desktop counts are runtime data. It also makes cleanup explicit: before rebuilding or leaving all-desktops mode, mark each dynamic icon invisible, detach or release its resources, and dispose it. Existing fixed icons are hidden in all-desktops mode and dynamically created icons are removed in navigation mode, so the two layouts cannot overlap.

### Centralize tray-mode synchronization

Replace the split icon refresh calls with a single mode-aware synchronization entry point. It determines the active mode, updates or rebuilds dynamic icons when needed, and updates visibility of the fixed icons.

Call this synchronization from initial UI setup, desktop-switch handling, desktop-count change detection, theme changes, and the Settings mode change handler. Desktop-count changes must marshal back to the UI thread before changing `NotifyIcon` objects. Numbered icon clicks invoke the existing direct-switch method with their captured zero-based index.

Refreshing all numbered icons on theme changes is required because their rendered images depend on theme and DPI. For normal desktop switches, icon count and labels do not change; synchronization must still retain the correct set and may update tooltip state without recreating icons unnecessarily.

### Keep contextual menu behavior consistent

All dynamic numbered icons use the existing `ContextMenuStrip`, so users retain Settings, About, Donate, Exit, and the dynamic desktop menu regardless of display mode. Left-click handling is attached specifically for all-desktops icons; the navigation current-number behavior remains unchanged.

### Hide mode-inapplicable controls in Settings

Add a tray layout radio group near the existing tray settings. The previous/next checkbox is visible only in navigation mode. The current-number Task View click option and desktop-name-initial option are also hidden in all-desktops mode because that layout is required to show exactly one icon per desktop and every left-click is reserved for direct switching.

Existing values are not erased while their controls are hidden. Returning to navigation restores their configured behavior.

## Risks / Trade-offs

- [Many virtual desktops create many notification-area icons and may overflow the visible tray] -> This is intrinsic to the requested all-desktops layout; Windows overflow behavior remains the platform mechanism, while all icons remain available to the user.
- [Frequent desktop-count polling could rebuild the tray from a worker thread] -> Rebuild only after an observed count change and marshal the work to the AppForm UI thread.
- [Stale dynamic icons can remain in Explorer or leak handles during mode/count changes] -> Always set `Visible` to `false` and dispose each removed `NotifyIcon`; perform the same cleanup on application shutdown.
- [The mode setting could contain a malformed value from a hand-edited config file] -> Validate at the mode boundary and fall back to `navigation`, preserving a usable default layout.
- [Existing controls could be visible at startup before settings state is applied] -> Initialize mode-dependent visibility after all radio values are loaded, while the form is still in its loading state.

## Migration Plan

1. Ship the new configuration setting with a `navigation` default.
2. Existing configuration files omit the setting and therefore retain navigation mode and their current previous/next preferences.
3. No data conversion is required; rollback consists of using a build that ignores the new setting, which returns the application to its legacy navigation layout.
