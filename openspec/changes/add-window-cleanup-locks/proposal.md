## Why

Users often establish a working set of windows on one virtual desktop, then open temporary windows that should be easy to discard. Closing those temporary windows individually is slow and makes it too easy to close an important working window by mistake.

## What Changes

- Add persistent cleanup-protection rules to the built-in window manager, represented by a lock icon beside each listed window.
- Add a `Lock All` action that protects every eligible window with resolved ownership across all virtual desktops.
- Add a `One-click Cleanup` action that, after confirmation, sends normal close requests only to currently eligible, unprotected windows across ordinary virtual desktops.
- Preserve per-window control by allowing users to toggle cleanup protection from the window row.
- Show clear status and outcome feedback while retaining Windows and application save prompts; the feature does not force-terminate processes.

## Capabilities

### New Capabilities
- `window-cleanup-locks`: Provides persistent protected-window rules and a safe all-desktop cleanup workflow in the built-in window manager.

### Modified Capabilities
- None.

## Impact

- Extends the WinForms window overview dialog with bulk-lock, cleanup, confirmation, row lock-state, and status UI.
- Adds an application-level cleanup-protection service with an application-name and window-title rule repository that uses the existing eligible-window enumerator and virtual-desktop ownership lookup.
- Reuses the existing normal window-close request path; adds no dependencies or process-termination behavior.
- Requires localized user-facing strings and documentation updates for the new workflow and its safety limits.
