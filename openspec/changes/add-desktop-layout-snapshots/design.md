## Context

The application is a .NET Framework 4.7.2 WinForms tray utility. Its existing window overview already uses `EnumWindows` to find eligible top-level windows, excludes shell/tool and this application's own windows, reads per-window virtual desktop GUIDs through the system virtual desktop manager, and moves a selected window through the same documented manager with a version-specific fallback. `VirtualDesktopRegistry` supplies the current ordered GUID and desktop-name view; supported private virtual desktop implementations already contain per-version creation contracts.

See [proposal.md](proposal.md) for motivation and the [desktop layout snapshot specification](specs/desktop-layout-snapshots/spec.md) for required behavior.

## Goals / Non-Goals

**Goals:**
- Keep snapshots portable across application restarts and resilient to transient HWNDs, PIDs, and virtual desktop GUID changes.
- Make matching deterministic and conservative so a restore never guesses which user window to move.
- Reuse the existing window eligibility and virtual desktop access paths, adding only the operations required for snapshot capture and restoration.
- Keep restore work and UI reporting responsive, with recoverable per-window errors.

**Non-Goals:**
- Recreate processes, documents, browser tabs, application state, window geometry, or monitor placement.
- Introduce automatic scheduling, synchronization, import/export, or snapshot backup.
- Delete desktops, reorder current desktops, or request user selection of ambiguous windows in the first release.
- Guarantee operations on elevated, protected, other-session, or Windows-special windows.

## Decisions

### Persist a versioned snapshot document below the existing application data location

Store a single versioned JSON document containing an ordered snapshot collection. A snapshot has an immutable ID, user-visible name, creation/update times, captured desktop descriptors (index, GUID, optional name), and captured window descriptors (desktop index plus process path, application ID when available, class name, title, process start time, and capture-only HWND/PID diagnostics).

Write updates atomically by serializing to a temporary sibling file and replacing the previous file only after a successful write. Read errors or unknown document versions leave existing windows untouched and surface an actionable UI error. A dedicated repository/service hides serialization and allows a later schema migration without mixing persistence with forms.

Using settings keys was rejected because snapshots are variable-length structured data and need independent schema/version handling. One file per snapshot was rejected because listing and mutation consistency would require directory reconciliation with little benefit for the expected small local collection.

### Expand the shared window record only with reusable identity metadata

Extend the existing eligible-window snapshot to retrieve the process executable path and start time when available, Win32 window class name, and AppUserModelID when the operating system provides it. Capture must tolerate access-denied or short-lived processes: unavailable optional fields stay empty rather than excluding an otherwise eligible window.

The snapshot service consumes this enriched record and records only windows whose desktop ownership can be identified and that can be moved by the current virtual desktop layer. Retaining the generic enumerator avoids a second, differently filtered capture implementation.

Persisting an HWND/PID alone was rejected because both identifiers are ephemeral. Requiring every optional identity field was rejected because it would unnecessarily exclude ordinary desktop applications.

### Resolve window identity with scored candidate sets and require a unique strongest match

On preview, enumerate current eligible windows once and compare each saved window only with candidates sharing the strongest available stable identity: normalized executable path first, then AppUserModelID, then process identity/class/title. Use title and process-start time as discriminators, not as sole permission to move unrelated applications. A candidate must have a sufficient score and a strictly higher score than every other candidate for the saved window; otherwise the saved window is `Not found` or `Ambiguous match`.

Reserve a current window when it becomes a unique match so one current window cannot be moved for two saved records. Perform analysis before desktop creation or window movement. This supports a preview whose counts match execution except for windows that disappear or change state afterward.

Exact HWND and PID are used only to enrich diagnostics for the same live process and are never treated as persistent identity. A loose title-only matcher and a “best available candidate” policy were rejected because they could move a user’s unrelated window.

### Resolve target desktops by current GUID first, then saved order, and create only the missing suffix

At restore time, take a fresh ordered current desktop list. Map a saved desktop to the current desktop with the saved GUID when present. For a saved GUID that no longer exists, use its saved zero-based order when that position exists. When the current list is shorter than the highest required saved position, create desktops until it exists, refresh the current mapping, and use that index. Never delete, rename, reorder, or otherwise modify extra desktops.

Expose a small creation operation behind the existing virtual desktop abstraction and implement it for every selected Windows API variant, where each already has a version-specific COM creation contract. Use the system `WindowDesktopLookup` GUID move first, then the existing implementation-specific mover only when the documented route cannot serve the resolved target.

GUID-only resolution was rejected because desktop IDs can change after a deletion or restart. Order-only resolution was rejected because a surviving GUID is a more faithful target when users add or rearrange desktops.

### Separate analysis, execution, and presentation models

Use immutable or append-only models for capture input, persisted snapshot data, restore preview items, and restore results. Preview items express `CanRestore`, `AlreadyCorrect`, `NotFound`, or `Ambiguous`; execution may add `Moved` and `Failed` with a reason. The restore controller first builds preview data, then executes only the preview's uniquely matched handles after explicit confirmation, revalidating the window and target desktop immediately before each move.

The management form owns reusable child dialogs for create/update preview, restore preview/progress/result, and delete/rename confirmation. Long-running enumeration and execution occur off the UI thread, with all control updates marshalled to the UI thread. The tray exposes a parent `Desktop Layout Snapshots` entry with `New Snapshot`, `Restore Most Recent`, and `Manage Snapshots` actions routed through `App`.

Moving during analysis was rejected because it prevents meaningful confirmation. Implementing restore state directly in form event handlers was rejected because matching, persistence, and result reporting need independently testable behavior.

New snapshot creation prepopulates a timestamped name. The user may change it, but persistence rejects an empty or whitespace-only name; this provides a predictable default while preventing unnamed records.

## Risks / Trade-offs

- [Windows virtual desktop private COM contracts differ by build] -> Add desktop creation through the selected abstraction and validate every supported implementation; report inability to create a target desktop as per-window failures rather than guessing.
- [Process path, start time, or AppUserModelID cannot be read] -> Treat fields as optional and require a unique conservative candidate before moving.
- [A browser or multi-document application has repeated window titles] -> Require a unique candidate score; classify ties as ambiguous and do not move either candidate.
- [A window or desktop changes after preview] -> Re-enumerate or validate at action time, record the changed item as failed/not found, and continue remaining items.
- [Snapshot file becomes corrupted] -> Use atomic writes, validate on load, show an error, and never infer data from a malformed document.
- [Restore could feel slow across many windows] -> Show total and current item progress, avoid per-window UI blocking, and continue after recoverable errors.

## Migration Plan

1. Introduce the snapshot document with an explicit schema version and create it only when a user saves their first snapshot; no existing configuration needs migration.
2. Add the tray submenu and management form without changing existing `All Windows...`, Task View, or desktop navigation commands.
3. On rollback, remove the entry points and new code while leaving the local snapshot document inert; it has no effect unless the feature is present.
