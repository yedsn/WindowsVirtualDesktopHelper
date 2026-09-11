# Windows Virtual Desktop Helper

Back to [Home](https://github.com/dankrusi/WindowsVirtualDesktopHelper)

## Settings Documentation

(as of v2.0)

### Settings

### All Windows Overview

Every notification-area icon has both an **All Windows...** command and a **Windows Task View** command in its right-click menu. The first opens a searchable snapshot of application windows across every virtual desktop, shown as separate desktop cards even when a desktop has no windows. Each window row includes its application icon, process identity, and window title. Select a window to activate it, drag it to another desktop card to move it, or use **Close Window** to request a normal single-window close after confirmation. Windows whose desktop cannot be identified are kept in an **Other windows** group. The overview does not provide batch close or force-terminate applications. Window moves use the system desktop API and a version-matched application-view fallback; protected or elevated windows may still be rejected by Windows.

|Config|Default|Description|
| --- | --- | --- |
| debug.singleInstance | ``true`` | If true, the app will prevent multiple instances of the app from starting.  Most users won't need to change this option. |
| general.startupWithWindows | ``false`` | If true, the app will register itself with Windows to startup when Windows starts (via the registry). |
| general.theme | ``"auto"`` | Can be either auto, dark or light. If set to auto, the theme is derived from the current windows theme (dark or light). |
| theme.icons.disabledOpacity | ``"0.5"`` | Defines the opacity to use for icons which are disabled. |
| theme.icons.font | ``"Segoe UI"`` | Defines the font name to use for the icons (for regular numbers, characters). If a specific style is to be used, then one can append 'Bold', 'Italic', 'Regular' after a comma and the font name - for example 'Arial, Bold'. |
| theme.icons.emojiFont | ``"Segoe UI Symbol"`` | Defines the font name to use for emoji icons. |
| theme.icons.symbolsFont | ``"Segoe UI Symbol"`` | Defines the font name to use for symbol icons. |
| theme.icons.iconBG.dark | ``"#0078D4"`` |  |
| theme.icons.iconFG.dark | ``"white"`` |  |
| theme.icons.iconBG.light | ``"#0078D4"`` |  |
| theme.icons.iconFG.light | ``"white"`` |  |
| theme.icons.symbolFG.dark | ``"black"`` | Defines the color to use for the previous/next desktop tray icons. |
| theme.icons.symbolFG.light | ``"black"`` | Defines the color to use for the previous/next desktop tray icons. |
| theme.overlay.width | ``900`` | With width in pixels of the switch overlay. |
| theme.overlay.height | ``430`` | With height in pixels of the switch overlay. |
| theme.overlay.font | ``"Segoe UI Light"`` | Defines the font name to use for the switch overlay. |
| theme.overlay.fontSize | ``30`` | Defines the font size to use for the switch overlay. |
| theme.overlay.overlayBG.dark | ``"black"`` |  |
| theme.overlay.overlayFG.dark | ``"white"`` |  |
| theme.overlay.overlayBG.light | ``"black"`` |  |
| theme.overlay.overlayFG.light | ``"white"`` |  |
| theme.status.width | ``250`` | With width in pixels of the status overlay. |
| theme.status.height | ``40`` | With height in pixels of the status overlay. |
| theme.status.offset | ``0`` | With height in pixels of the status overlay. |
| theme.status.font | ``"Segoe UI Light"`` | Defines the font name to use for the status overlay. |
| theme.status.fontSize | ``12`` | Defines the font size to use for the status overlay. |
| theme.status.overlayBG.dark | ``"black"`` |  |
| theme.status.overlayFG.dark | ``"white"`` |  |
| theme.status.overlayBG.light | ``"black"`` |  |
| theme.status.overlayFG.light | ``"white"`` |  |
| feature.showSplashScreen | ``true`` | If enabled, a splash screen is shown on startup of the app. Overlays must be enabled. |
| feature.showSplashScreen.duration | ``2000`` | Splash duration in milliseconds. |
| feature.showSplashScreen.text | ``"Virtual Desktop Helper"`` | The splash text to show. |
| feature.iconTray.desktopDisplayMode | ``"navigation"`` | Controls the desktop tray layout. Use ``"navigation"`` for the previous, current, and next controls, or ``"all-desktops"`` to show one numbered icon per virtual desktop plus a Desktop Manager icon. In ``"all-desktops"`` mode, left-clicking a numbered icon switches directly to that desktop; Desktop Manager opens the configured window manager. |
| feature.iconTray.windowManager | ``"system"`` | Controls the window manager opened by the tray manager icon: ``"system"`` opens Windows Task View; ``"built-in"`` opens the All Windows overview. |
| feature.showPrevNextIcons | ``true`` | If enabled, a previous and next arrow will appear in the icons tray of Windows to allow easy switching between desktops. |
| feature.showPrevNextIcons.automaticallyHidePrevNextOnBounds | ``false`` | If enabled, the prev/next icon will automatically hide if there is no prev/next desktop. |
| feature.showPrevNextIcons.nextChar | ``"\u203A"`` | Defines the character to use for next desktop icon (typically a unicode character like the chevron, for example \xE101 = skip forward (player style),  = next (arrow style), \xe26b = next (chevron style), \u02C3 = next (chevron style), \u203A = next (chevron style)) |
| feature.showPrevNextIcons.prevChar | ``"\u2039"`` | Defines the character to use for prev desktop icon (typically a unicode character like the chevron, for example \xE100 = skip back (player style), \xE112 = previous (arrow style), \xe26c = previous (chevron style), \u02C2 = previous (chevron style), \u2039 = previous (chevron style)) |
| feature.showDesktopSwitchOverlay | ``true`` |  |
| feature.showDesktopSwitchOverlay.duration | ``2000`` | Defines the duration in milliseconds for a switch overlay to show. If set to zero, then the overlay is shown indefinately. |
| feature.showDesktopSwitchOverlay.animate | ``true`` |  |
| feature.showDesktopSwitchOverlay.translucent | ``true`` |  |
| feature.showDesktopSwitchOverlay.showOnAllMonitors | ``true`` |  |
| feature.showDesktopSwitchOverlay.position | ``"middlecenter"`` |  |
| feature.showDesktopStatusOverlay | ``false`` |  |
| feature.showDesktopStatusOverlay.animate | ``true`` |  |
| feature.showDesktopStatusOverlay.translucent | ``true`` |  |
| feature.showDesktopStatusOverlay.showOnAllMonitors | ``true`` |  |
| feature.showDesktopStatusOverlay.position | ``"topcenter"`` |  |
| feature.useHotKeyToJumpToDesktopNumber | ``false`` |  |
| feature.useHotKeyToJumpToDesktopNumber.hotkey | ``"Alt"`` |  |
| feature.useHotKeyToJumpToPreviousDesktop | ``false`` |  |
| feature.useHotKeyToJumpToPreviousDesktop.hotkey | ``"Alt + Tilde"`` |  |
| feature.useHotKeyToSwitchDesktopForward | ``false`` |  |
| feature.useHotKeyToSwitchDesktopForward.hotkey | ``"Alt + Right"`` |  |
| feature.useHotKeyToSwitchDesktopBackward | ``false`` |  |
| feature.useHotKeyToSwitchDesktopBackward.hotkey | ``"Alt + Left"`` |  |
| feature.useHotKeyToOpenTaskView | ``false`` | Uses the configured window manager. |
| feature.useHotKeyToOpenTaskView.hotkey | ``"Alt + D"`` | Hotkey for the configured window manager. |
| feature.showDesktopNumberInIconTray | ``true`` |  |
| feature.showDesktopNameInIconTray | ``false`` |  |

### Config File

This is located in ``%appdata%\WindowsVirtualDesktopHelper`` (for example ``C:\Users\<USER>\AppData\Roaming\WindowsVirtualDesktopHelper``)
as a ``.config`` file, and can be edited with any text editor.

Note: configuration lines that start with ``#`` are comments and ignored by the configuration system.

### Configuration Backup

Open **Settings** and choose **Export Backup** to create one portable backup file. The backup includes saved application settings and custom hotkeys, the current virtual desktop names and order, and every saved desktop layout snapshot with its window matching rules.

Choose **Import Backup** from Settings, select a backup file, review its counts, and confirm the replacement. Import replaces the saved application settings and snapshot collection. It creates missing virtual desktops and restores names for the backed-up desktop positions. Extra local desktops are retained unchanged. Import does not start, close, move, or otherwise change open application windows; restore an imported snapshot separately when you want to restore its window layout.

If the backup cannot be read or is from an unsupported format version, no local data changes. The startup-with-Windows registration is not changed during import; confirm that option in Settings or restart the application after import if it needs to be reconciled.

### Command Line Arguments

The app can be run with command line arguments to specificy any configuration setting. For example, one could
run the app with the following command line arguments:

```
WindowsVirtualDesktopHelper.exe --theme.overlay.overlayBG.dark "red" --feature.showPrevNextIcons.nextChar "]" --feature.showPrevNextIcons.prevChar "["
```

Command line arguments take precedence over the config file settings.

### Custom Hotkey Settings

See [Hotkeys Documentation](https://github.com/dankrusi/WindowsVirtualDesktopHelper/blob/main/Documentation/Hotkeys.md)
for more information on how to define custom hotkeys.
