## Context

See [proposal.md](proposal.md) for motivation and [window-cleanup-locks specification](specs/window-cleanup-locks/spec.md) for behavior. The existing `WindowOverviewForm` displays a snapshot of eligible top-level windows across virtual desktops. It already uses `WindowEnumerator` for eligibility and normal `WM_CLOSE` requests, and `App` maps each HWND to a desktop index through `WindowDesktopLookup`.

The overview is reusable and lists several desktops at once. `Lock All` and cleanup apply to every window with resolved ownership across those desktops. Existing desktop-layout snapshots are persisted, cross-restart data intended to restore placement; their desktop-placement semantics do not fit cleanup protection, so cleanup locks use a separate rule repository.

## Goals / Non-Goals

**Goals:**
- Keep cleanup protection persistent across helper restarts and allow it to be toggled from the overview.
- Make bulk lock and cleanup operations safe against stale UI snapshots and disappearing windows.
- Reuse the existing top-level-window filter and normal close path so cleanup follows the same eligibility and unsaved-work behavior as single-window close.

**Non-Goals:**
- Create named cleanup-lock snapshots, restore windows, terminate processes, or close background processes with no eligible top-level window.
- Apply cleanup to every desktop, unresolved desktop ownership, or windows shown on all desktops.
- Block a protected window from being manually closed, moved, activated, or otherwise changed outside cleanup.

## Decisions

### Persist exact application-name and window-title protection rules

Add an application-level service backed by an atomic JSON repository under the application's data directory. Each rule contains the observed application process name and window title. The overview uses exact, case-insensitive matching for both fields, so a matching window remains protected after the helper restarts while other windows from the same application remain eligible for cleanup.

Persisting an HWND was rejected because Windows can reuse it and it is invalid after a restart. A process-name-only rule was rejected because it would unexpectedly protect every window from that application. Reusing desktop layout snapshots was rejected because their saved desktop mapping and restore-oriented identity fields would make routine cleanup protection difficult to inspect and maintain.

### Resolve ownership for all bulk actions

`Lock All` and cleanup take fresh snapshots of every eligible window whose desktop GUID resolves to an ordinary virtual desktop. Cleanup shows this all-desktop target list for confirmation, then re-enumerates each item before requesting close to ensure it is still assigned to a normal virtual desktop and remains unlocked. Windows that are unresolved or shown on all desktops are never bulk-lock or batch-cleanup targets.

This prevents a stale overview list from closing a disappeared or no-longer-eligible window and lets both bulk actions operate on the complete working set shown by the form.

### Expose the lock state beside the application identity and make it directly toggleable

The overview's list-row presentation will add a distinct locked/unlocked state image immediately beside the application icon/name. The hit area for that state image toggles only the selected row's protection state; a keyboard-accessible command provides the same action for users who do not use the pointer. Row rebuilds and refreshes obtain the displayed state from the application-level service.

Replacing the existing grouped `ListView` with a custom card control was rejected because it would unnecessarily rewrite searching, selection, drag-to-desktop, keyboard navigation, and the established overview layout. A text suffix such as `[Locked]` was rejected because the user requested a recognizable lock icon and it makes scanning dense lists worse.

### Confirm a fresh target count, then perform normal closes as best effort

The form displays the current snapshot's number of eligible, unlocked windows across all ordinary virtual desktops inside the `One-click Cleanup` button label, using red text when the number is greater than zero. When the user presses `One-click Cleanup`, the form first obtains a fresh all-desktop target list. If it is zero, it shows a non-destructive status message. Otherwise it presents a scrollable confirmation dialog listing every target's application and window title. On confirmation, it enumerates again and posts a normal close request to each remaining target in a background STA operation, revalidating the live-window key and resolved desktop ownership before every request. The UI disables duplicate bulk action requests while work is in flight, then refreshes and reports sent and failed request counts.

No forced process kill is permitted: `WM_CLOSE` lets every application keep its normal save and shutdown behavior. Treating a successfully posted request as a confirmed closure was rejected because applications can cancel or defer closing; the outcome reports requests sent, not processes terminated. Sending cleanup work synchronously on the UI thread was rejected to keep the form responsive if enumeration or shell ownership queries are slow.

### Keep individual close behavior unchanged

Existing selected-window Close remains available and is not blocked by cleanup protection. It continues to request confirmation for exactly one selected window. Protection affects only `One-click Cleanup`, which prevents the lock icon from implying an operating-system window lock.

Disabling individual close for protected windows was rejected because that changes an existing command unexpectedly and conflates cleanup protection with preventing user action.

## Risks / Trade-offs

- [A saved rule no longer describes a desired window] -> Rules are exact application-name and window-title matches and can be toggled off from any matching overview row.
- [A target moves or the user changes desktops around confirmation] -> Re-enumerate after confirmation and require current assignment to the current target desktop before sending a close request.
- [An application ignores, delays, or cancels `WM_CLOSE`] -> Report only requested closes, refresh the overview, and leave any still-open window visible.
- [A large set of close requests generates multiple application dialogs] -> Require explicit count-based confirmation and preserve each target application's normal prompt behavior.
- [Native list state-image interaction is less obvious than a button] -> Use clearly differentiated lock imagery, tooltip/accessibility text, and keyboard toggle support.

## Migration Plan

1. Add the persistent rule repository and overview controls without changing existing settings or snapshots.
2. Existing users begin with an empty rule collection; new locks are saved immediately and loaded at startup.
3. Rollback removes the form actions and rule repository; the separate rule file can be removed without affecting other application data.
