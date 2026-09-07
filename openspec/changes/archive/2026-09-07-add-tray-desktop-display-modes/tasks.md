## 1. Configuration And Settings UI

- [x] 1.1 Add the persisted `feature.iconTray.desktopDisplayMode` setting with `navigation` as the default, validate unknown values by falling back to `navigation`, and verify the generated settings documentation includes both supported values
- [x] 1.2 Add mutually exclusive navigation and all-desktops radio controls to the Settings form, load and save the selected mode, and verify an existing configuration without the new key opens in navigation mode
- [x] 1.3 Update mode-dependent Settings visibility so previous/next controls and other navigation-only tray options are hidden in all-desktops mode and restored in navigation mode; verify the visibility changes immediately when either radio control is selected

## 2. Tray Icon Lifecycle And Interaction

- [x] 2.1 Add an owned collection for dynamic per-desktop `NotifyIcon` instances and implement cleanup that hides and disposes every removed icon; verify no dynamic icons remain after switching back to navigation mode or exiting
- [x] 2.2 Implement all-desktops icon creation and refresh for the current desktop count, including numbered labels, shared context menu, theme-aware generated icons, and captured zero-based desktop indexes; verify four available desktops produce exactly four numbered tray icons
- [x] 2.3 Implement left-click handling for dynamic numbered icons so each icon directly switches to its represented desktop; verify clicking desktop 3 invokes the existing direct desktop-switch behavior with index 2
- [x] 2.4 Integrate mode-aware synchronization with the existing fixed icons so navigation mode preserves current-number, optional name, previous, and next behavior while all-desktops mode hides those fixed icons; verify the two layouts never appear simultaneously

## 3. Runtime Synchronization

- [x] 3.1 Invoke tray synchronization after startup, desktop switches, display-count changes, mode changes, and system-theme changes; marshal all NotifyIcon mutations to the AppForm UI thread and verify desktop additions/removals update without restarting
- [x] 3.2 Ensure all-desktops icon count and numbering remain correct when the desktop count decreases, including removal of icons for deleted desktops; verify a change from four desktops to three leaves only icons 1 through 3
- [x] 3.3 Preserve existing right-click context-menu actions and dynamic desktop menu behavior for numbered icons; verify Settings, About, Donate, Exit, and desktop selection remain available

## 4. Documentation And Verification

- [x] 4.1 Update `Documentation/Settings.md` with the new setting, supported values, default, and mode-specific behavior; verify the documentation matches the implemented configuration key
- [x] 4.2 Build the solution and resolve compilation or designer wiring errors introduced by the new controls and event handlers
- [x] 4.3 Perform a Windows runtime verification covering both modes, left-click direct switching, mode changes, desktop count changes, theme refresh, context menus, and clean application exit
