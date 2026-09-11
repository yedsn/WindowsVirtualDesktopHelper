## 1. Window Discovery and Desktop Mapping

- [x] 1.1 Add a shared top-level application window snapshot model and user32 enumeration utility that captures HWND, process identity, title, and eligible-window state; verify it excludes hidden and shell/tool windows while retaining normal application windows.
- [x] 1.2 Add safe shared helpers to validate an HWND, foreground an existing window, and send it a normal close request; verify a closed HWND is rejected without throwing and close requests do not terminate the process directly.
- [x] 1.3 Extend `IVirtualDesktopManager` with ordered virtual desktop identity lookup and implement it for every supported Windows desktop API variant; verify the selected implementation maps the current desktop identity to `Current()`.
- [x] 1.4 Build the cross-desktop window snapshot service that joins each eligible window's system desktop ID to the ordered desktop identities and yields an explicit unresolved result on lookup failures; verify one failed mapping does not omit other windows.

## 2. Overview Dialog

- [x] 2.1 Create a reusable `WindowOverviewForm` with a grouped window list, search input, refresh control, activation control, close control, and status area; verify the project includes the form and it opens without displaying the hidden tray host form.
- [x] 2.2 Bind fresh snapshots to desktop groups and an `Other windows` group, including application identity and window title for every row; verify windows from multiple desktops appear in separate groups and unresolved windows remain visible.
- [x] 2.3 Implement case-insensitive search across process identity and title while preserving containing desktop groups; verify filtering and clearing the query update the visible rows correctly.
- [x] 2.4 Implement selected-window activation with handle validation, destination desktop switching, foreground attempt, and nonfatal status feedback; verify activation of a current-desktop window and a non-current-desktop window both leave the dialog usable.
- [x] 2.5 Implement single-window close confirmation followed by a normal close request and refresh; verify cancel leaves the window unchanged and confirm refreshes the snapshot without offering batch close.

## 3. Tray Integration and Documentation

- [x] 3.1 Add the fixed `All Windows...` command to the shared tray context menu and route it through `App` to show or focus the reusable overview; verify it is available from both navigation and all-desktops tray modes.
- [x] 3.2 Update user documentation to describe the new tray command, overview grouping, unresolved-window fallback, and single-window cleanup behavior; verify the documented command text matches the UI.

## 4. Verification

- [x] 4.1 Build the solution in Debug configuration and resolve compilation warnings or errors introduced by the change; verify the build succeeds.
- [x] 4.2 Validate a multi-desktop session using temporary application windows: confirm the overview groups mapped windows from current and non-current desktops, search retains a matching window, activation reports a nonfatal status when Windows foreground policy denies it, and a normal close request closes the selected temporary window. Verify existing Task View and desktop switching code paths remain unchanged.
