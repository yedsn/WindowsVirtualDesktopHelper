## Why

Windows Task View exposes the windows for only one virtual desktop at a time. Users who keep work split across several desktops need a single view to find and close forgotten applications without switching through every desktop.

## What Changes

- Add an `All Windows...` item to the tray context menu.
- Add a window overview dialog that lists eligible top-level application windows from every virtual desktop, grouped by desktop.
- Support searching the overview, refreshing its snapshot, activating a selected window, and closing one selected window after confirmation.
- Preserve discoverability when virtual-desktop ownership cannot be resolved by placing those windows in an explicit fallback group.

## Capabilities

### New Capabilities
- `window-overview`: Provides a cross-virtual-desktop view of application windows and safe single-window cleanup actions.

### Modified Capabilities
- None.

## Impact

- Affects the tray menu and adds a WinForms overview dialog.
- Adds a shared Windows window-enumeration utility and a virtual-desktop ownership query to the internal virtual desktop abstraction.
- Requires updates to every supported virtual desktop API implementation because their non-public COM contracts differ by Windows release.
- Does not add external dependencies or alter existing desktop-switching behavior.
