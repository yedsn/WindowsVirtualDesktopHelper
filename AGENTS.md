# Repository Guidelines

## Project Structure & Module Organization

`Source/` contains the .NET Framework 4.7.2 WinForms application. Keep tray-host and application coordination in `Source/App/`, forms in `Source/Forms/`, Windows virtual-desktop compatibility code in `Source/VirtualDesktopAPI/`, and shared helpers in `Source/Util/`. Input and hotkey support live in `WindowsInputAPI/` and `WindowsHotKeyAPI/`. The solution is `WindowsVirtualDesktopHelper.sln`; `Setup/` builds the MSI installer. User-facing documentation belongs in `Documentation/`, release/deployment tooling in `Scripts/`, and visual assets in `Images/`.

## Build, Test, and Development Commands

Use Visual Studio 2022 or newer with the **.NET desktop development** workload. Open `WindowsVirtualDesktopHelper.sln` and build the `Debug | Any CPU` configuration for normal development.

From a Developer PowerShell, build without the release signing step:

```powershell
msbuild WindowsVirtualDesktopHelper.sln /t:Rebuild /p:Configuration=Debug /p:Platform='Any CPU'
```

For a locally deployable unsigned Release archive, set `LOCAL_DEPLOY_DIR` in `.env` and run:

```powershell
.\Scripts\deploy-local.ps1
```

Do not run Release builds that invoke code-signing scripts unless the required certificate is available.

## Coding Style & Naming Conventions

Follow `Source/.editorconfig`: tabs, four-column tab width, CRLF endings, block-scoped namespaces, and braces on the same line as declarations. Use PascalCase for types, methods, properties, and events; interfaces begin with `I`. Match surrounding code rather than reformatting unrelated files. Add new `.cs` files explicitly to `Source/WindowsVirtualDesktopHelper.csproj`.

## Testing Guidelines

There is no automated test project. Build the affected configuration and manually validate user-visible workflows on supported Windows 10/11 versions. For tray or virtual-desktop changes, verify icon updates, menu actions, desktop switching, and error handling. Record manual verification steps in the pull request.

## Commit & Pull Request Guidelines

Recent history uses short, imperative subjects, including Chinese summaries and conventional release messages such as `release: v2.2.0` or `feat: improve window manager interaction`. Keep commits focused. Pull requests should state the behavioral change, list verification performed, link relevant issues/specs, and include screenshots for WinForms UI changes. Do not commit generated `bin/`, `obj/`, packages, signing artifacts, or local `.env` settings.

## Configuration and Releases

Runtime configuration is stored under `%APPDATA%\WindowsVirtualDesktopHelper`. Preserve backward compatibility for setting keys. Use `Scripts/release.ps1 -Version X.Y.Z -Branch main -Push` only for intentional releases: it updates assembly versions, commits all staged work, creates a tag, and can push every configured remote.

## Debug Process Restart

After completing any code or configuration change, restart the Debug application process used by the `Debug WindowsVirtualDesktopHelper` entry in `.vscode/launch.json`. Run its equivalent sequence: stop `WindowsVirtualDesktopHelper`, build `Debug | Any CPU` with the release post-build event disabled, then start `Source\bin\Debug\WindowsVirtualDesktopHelper.exe` with that directory as the working directory. Confirm that a new process is running before reporting completion.
