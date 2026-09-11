# Windows Virtual Desktop Helper

Simple and lightweight app to help with Virtual Desktops for Windows 10 and Windows 11.

![Screenshot](Images/WindowsVirtualDeskopHelper%20Screenshot.png)

[Download v2.0 Setup (.msi)](https://github.com/dankrusi/WindowsVirtualDesktopHelper/releases/download/v2.0/WindowsVirtualDesktopHelper.Setup.v2.0.msi) | 
[Download v2.0 Executable (.zip)](https://github.com/dankrusi/WindowsVirtualDesktopHelper/releases/download/v2.0/WindowsVirtualDesktopHelper.Executable.v2.0.zip)

Windows comes builtin with virtual desktops, however some important features are missing, such
as displaying which desktop you are on when switching. Windows Virtual Desktop Helper helps
fix some of these missing features.

Keywords: Virtual Desktop indicator, Virtual Desktop name, Virtual Desktop number

Note: Currently Windows Virtual Desktop Helper is not code-signed, and may be reported as untrusted by Windows
Defender or other anti-virus applications. Typically, after enough users download, install, and report
the software as okay/safe, this warning will go away.



## ⚡ Features

* Show desktop number in notification area
* Show desktop name when switching desktops
* Show prev/next desktop by clicking icons in notification area
* Show desktop initial in notification area
* View and clean up application windows across all virtual desktops from the tray menu
* Save and safely restore named desktop layout snapshots for already-open application windows
* Custom hot keys for virtual desktop actions
* Autostart with Windows
* Simple and lightweight
* Configurable
* Free

![Settings](Images/WindowsVirtualDeskopHelper%20Settings.png)



## 🚀 Installation

### Requirements

Windows Virtual Desktop Helper needs the Microsoft .NET Framework 4.7 or higher in order to run. Most likely you already have this installed, otherwise you can get it from [dotnet.microsoft.com](https://dotnet.microsoft.com/en-us/download/dotnet-framework)

### Setup

You can install Windows Virtual Desktop Helper to your system using the setup program.

[Download WindowsVirtualDesktopHelper Setup v2.0.msi](https://github.com/dankrusi/WindowsVirtualDesktopHelper/releases/download/v2.0/WindowsVirtualDesktopHelper.Setup.v2.0.msi)

Note: Currently Windows Virtual Desktop Helper is not code-signed, and may be reported as malware by Windows
Defender or other anti-virus applications. Typically, after enough users download, install, and report
the software as okay/safe, this malware warning will go away. If you prefer to avoid some of these issues, use the Zip version of the executable instead.

### Executable

You can just run the executable file WindowsVirtualDesktopHelper.exe to use Windows Virtual Desktop Helper.

[Download WindowsVirtualDesktopHelper Executable v2.0.zip](https://github.com/dankrusi/WindowsVirtualDesktopHelper/releases/download/v2.0/WindowsVirtualDesktopHelper.Executable.v2.0.zip)

### Scoop

A command-line installer for Windows

[https://scoop.sh/](https://scoop.sh/)

```scoop bucket add extras```

```scoop install windows-virtualdesktop-helper```



## 🎁 Donate

Do you like Windows Virtual Desktop Helper? 

[Donate via PayPal](https://www.paypal.com/donate/?hosted_button_id=BG5FYMAHFG9V6)



## 💻 Compatibility

Currently this app is compatible with Windows 10 and Windows 11. 
Microsoft is constantly changing their API for the Virtual Desktops, so it appears not all builds of Windows are fully working yet.
Please note that if you use bleeding-edge insider buildes, this app may not yet be compatible.

The following versions are not supported:

* Windows 7
* Windows 8
* Windows Server
  
If you know of a working version or non-working version, please report it.



## 🗺️ Roadmap

While the goal of this app is to remain simple and lightweight, there are some features that will still be added:

* Settings: more configuration options ✔️
* Console-Mode: allow a console-mode which can be used by scripts

Technical Roadmap:

- Refactor settings from .net settings loader to own system (the .net system is so bloated and crappy) ✔️
- Add support for setting settings via command line ✔️
- Split out settings UI and app UI ✔️
- =============
- Make features more modular

 

## 📜 Changelog

See [CHANGELOG.md](https://github.com/dankrusi/WindowsVirtualDesktopHelper/blob/main/CHANGELOG.md)



## 🤔 Frequently Asked Questions

### Help! This App doesn't work on my version of Windows 11

Microsoft is constantly changing the Windows APIs for managing virtual desktops - thus usually with each new version we have to write a new API wrapper to interface with Windows. This takes time, and sometimes is not easy to integrate with. Don't expect immediate support for un-released versions of Windows (like Insider).

### Where is the icon showing the screen number?

Windows automatically organizes the notification area icons, and places new ones in the icons menu under the ^ chevron. You can drag the screen number into the main icon area from there to have it always showing.

### Why are the prev/next screen icons in the wrong order?

Windows automatically organizes and orders the notification area icons. You can drag prev/next screen icons to organize them accordingly.

### Why is this app is being reported by Windows Defender as untrusted?

Currently Windows Virtual Desktop Helper is not code-signed, and thus may be reported as untrusted. With USD 70 donations per year, the app will be signed.

### Why is this app is being reported by Windows Defender as malware?

Currently Windows Virtual Desktop Helper is not code-signed, and due to its installation option for installing to autostart, may be flagged as malware. Typically, after enough users download, install, and report
the software as okay/safe, this malware warning will go away. With USD 70 donations per year, the app will be signed.

### Why is the app based on the older .NET 4.7?

The idea is to make the app as easy and lightweight to run as possible. Most systems have some version of .NET installed, thus we use a low version to cover as many users as possible. 



## ⚙️ Configuration

The most common and basic settings can be configured using the GUI Settings. 

For more advanced configurations and features you can use the config file or command line arguments (as of v2.0):

For example, one can set a hotkey to jump to a specific desktop by setting the following setting:

```
hotkeys.myCustomHotkey1 = "Ctrl + Alt + Shift + D1 = Desktop1"
hotkeys.myCustomHotkey2 = "Ctrl + Alt + Shift + D2 = Desktop2"
```

or use a custom shortcut for prev/next desktop:

```
hotkeys.myCustomKey1: "Alt + W = DesktopForward"
hotkeys.myCustomKey2: "Alt + Q = DesktopBackward"
```

See [Hotkeys Documentation](https://github.com/dankrusi/WindowsVirtualDesktopHelper/blob/main/Documentation/Hotkeys.md) for more info.

### Settings

See [Settings Documentation](https://github.com/dankrusi/WindowsVirtualDesktopHelper/blob/main/Documentation/Settings.md)
for a full list of all settings.

### Config File

This is located in ``%appdata%\WindowsVirtualDesktopHelper`` (for example ``C:\Users\<USER>\AppData\Roaming\WindowsVirtualDesktopHelper``)
as a ``.config`` file, and can be edited with any text editor.

Note: configuration lines that start with ``#`` are comments and ignored by the configuration system.

### Command Line Arguments

The app can be run with command line arguments to specificy any configuration setting. For example, one could
run the app with the following command line arguments:

```
WindowsVirtualDesktopHelper.exe --theme.overlay.overlayBG.dark "red" --feature.showPrevNextIcons.nextChar "]" --feature.showPrevNextIcons.prevChar "["
```

Command line arguments take precedence over the config file settings.

### Desktop Layout Snapshots

Use the tray menu's **Desktop Layout Snapshots** submenu to save the current assignment of eligible application windows to virtual desktops. A saved snapshot includes window identity information and the desktop layout, but does not save files, browser tabs, application content, window size, or position.

Restoring a snapshot first shows a preview. The app moves only an already-open window with one reliable match; it leaves missing and ambiguous windows unchanged. It never starts or closes applications, never moves windows outside the selected snapshot, and never deletes current virtual desktops. Missing target desktops are created when required, while any extra current desktops are preserved. The restore result provides a per-window explanation for moved, already-correct, missing, ambiguous, and failed items.

For applications with dynamic window titles, select a snapshot and choose **Edit Window Rules**. You can edit the saved **Target desktop**, **Window**, and **Application** fields. Target desktop controls where a matched window is moved. Edit the saved **Window** name directly, then enable **Use regular expression** when the title changes. When the option is disabled, the window name is matched exactly. Changing **Application** uses the entered process name for matching.

Choose **Delete Rule** to remove a selected window from the snapshot. The application window is not closed or changed; it is simply excluded from future restores of that snapshot.

| Rule | Matches |
| --- | --- |
| `Project Alpha` | Any title containing `Project Alpha` |
| `^Project Alpha.*` | A title starting with `Project Alpha` |
| `^Project Alpha.* - Visual Studio$` | The full Visual Studio title, including a variable middle section |

A regular expression only narrows matches for the same application identity; it does not match unrelated applications. If a rule matches more than one eligible window, the restore preview marks it ambiguous and does not move any of them.

### Configuration Backup

The **Settings** window provides **Export Backup** and **Import Backup**. A backup contains saved settings and custom hotkeys, virtual desktop names and order, and all saved snapshots with their matching rules. Before import, the app shows the backup's date and item counts for confirmation.

Import replaces the application's saved settings and snapshot collection, creates only missing desktops, and restores names for the backed-up desktop positions. It keeps any additional local desktops untouched and never moves, starts, or closes application windows. Use a restored snapshot separately if you want to restore window placement.



## 🔧 How it works

This program works by using some unofficial/undocumented Windows APIs which Windows uses internally to manage the desktop.
The unofficial nature of these APIs is very unfortunate, because it means that each time Windows 11 makes an update, we have
to reverse engineer the APIs and their undocumented COM CLSIDs - which is tedious and wastes a lot of time.

This is why the maintainers are reluctant to add too many other features, because its enough of a task to keep
up with all the Windows updates for the basic features.



## ⚒ Building & Contributing

Install Visual Studio 2022 or later with ".NET desktop development" feature set, and open the solution file WindowsVirtualDesktopHelper.sln. You can then build the project.

### Publishing a release

In VS Code, run the `release` launch configuration. It prompts for a version and branch; the default version increments the current patch version and the default branch is `main`.

To use it from a terminal, run:

```powershell
.\Scripts\release.ps1 -Push
```

Use `-Version 2.2.0` and `-Branch main` to bypass either prompt. The command updates the application version, commits the current working-tree changes, creates a `v2.2.0` tag, then pushes the branch and tag to every configured Git remote. The tag triggers the GitHub Actions release workflow.

Note: The Setup project which creates the MSI installer will require the following extension to be installed: [Microsoft Visual Studio Installer Projects 2022](https://marketplace.visualstudio.com/items?itemName=VisualStudioClient.MicrosoftVisualStudio2022InstallerProjects)

Note: Building in Release mode will automatically sign the executable with the designated code-signing certificate, which will not work on your machine. If you really must build your own release, you can remove the post-build event.

### Depencencies

This project doesn't have any dependencies or external libraries, and we expect it to stay this way. We want the app
to remain a single .exe file which can be run without any installation or dependencies.

### The Hunt for CLSIDs

TODO

### Your help is wanted!

TODO



## 🙏 Thanks

Many thanks for the original API work done by [MScholtes](https://github.com/MScholtes) and contributions by [Flaflo](https://github.com/Flaflo).

Thanks to the following contributors:
 - [y2nd66](https://github.com/y2nd66)
 - [SleepyBag](https://github.com/SleepyBag)
 - [yossdev](https://github.com/yossdev)
 - [MadoScientist97](https://github.com/MadoScientist97)
 - [...and more](https://github.com/dankrusi/WindowsVirtualDesktopHelper/pulls?q=is%3Apr+is%3Aclosed)


	 
## 🤩 You might also like...

### Windows Taskbar Helpers

A free utility to make the Windows 11 Taskbar just a little bit more useful by allowing you to pin quick launchers that always open a new folder, app or anything.

[![Screenshot](https://raw.githubusercontent.com/dankrusi/WindowsTaskbarHelpers/main/Images/WindowsTaskbarHelpers_Screenshots_v1.png)](https://github.com/dankrusi/WindowsTaskbarHelpers)

[github.com/dankrusi/WindowsTaskbarHelpers](https://github.com/dankrusi/WindowsTaskbarHelpers)

### LitePDF

A free and lightweight no-BS PDF viewer. And no, it doesn't have AI.

[![Screenshot](https://raw.githubusercontent.com/dankrusi/LitePDF/main/Images/Screenshots/Win11/LtiePDF_Screenshot_Win11_A.png)](https://github.com/dankrusi/LitePDF)

[github.com/dankrusi/LitePDF](https://github.com/dankrusi/LitePDF)
