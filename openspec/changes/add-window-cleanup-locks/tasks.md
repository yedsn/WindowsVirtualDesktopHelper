## 1. Cleanup Protection Foundation

- [x] 1.1 Add an application-level persistent cleanup-lock rule service keyed by application name and window title, with atomic storage and toggle/query operations; verify rules survive restart and do not protect a same-application window with a different title.
- [x] 1.2 Add application-layer operations that enumerate eligible windows across resolved desktops for Lock All and cleanup, and build a fresh unprotected cleanup target set; verify unresolved and all-desktop windows are excluded from bulk lock and cleanup.
- [x] 1.3 Add best-effort batch normal-close execution that validates every live target before requesting close and returns sent/failed outcome counts; verify one unavailable target does not prevent requests to the remaining targets and no process is forcibly terminated.

## 2. Window Manager Controls

- [x] 2.1 Extend the window overview with `Lock All` and `One-click Cleanup` controls, localized as `全部锁定` and `一键清理`; verify both actions operate across every resolved desktop shown in the overview.
- [x] 2.2 Display a clear locked/unlocked cleanup-protection icon beside every eligible window row, with pointer toggle, tooltip/accessibility text, and keyboard-accessible toggle behavior; verify refreshes preserve a live window's state and individual protection changes update the display.
- [x] 2.3 Implement `Lock All` as an idempotent action and refresh the overview afterwards; verify a second invocation retains prior protection and protects a newly opened eligible window on any desktop.
- [x] 2.4 Display the current snapshot's all-desktop cleanup target count inside `One-click Cleanup`, in red when greater than zero; list every cleanup target's application and window title before confirmation; implement one-click cleanup target preview and explicit confirmation that identifies the all-desktop target count; verify cancellation makes no close request and a zero-target invocation reports status without destructive confirmation.
- [x] 2.5 Run confirmed cleanup off the UI thread, prevent duplicate bulk actions while it runs, refresh the overview when complete, and report sent/failed request counts; verify the form remains usable and a window that remains open after declining its own save prompt stays visible after refresh.
- [x] 2.6 Preserve selected-window Close, activation, search, keyboard navigation, and drag-to-desktop behavior for both protected and unprotected rows; verify protection affects only one-click cleanup.

## 3. Localization and Documentation

- [x] 3.1 Add English and Chinese localized strings for lock states, bulk-action labels, confirmations, empty cleanup status, and cleanup outcomes; verify the overview updates text correctly after changing application language.
- [x] 3.2 Update user documentation to explain that `Lock All` and one-click cleanup apply across resolved virtual desktops, locks persist as application-name and window-title rules, and cleanup sends normal close requests that can show application save prompts; verify command names match the delivered UI.

## 4. Verification

- [x] 4.1 Build `Debug | Any CPU` with the release post-build event disabled; verify the solution compiles without errors introduced by the change.
- [ ] 4.2 Manually validate a multi-desktop session: lock windows across desktops, open temporary windows on both desktops, toggle one lock, confirm cleanup, and verify every ordinary desktop's unlocked windows receive close requests while protected, unresolved, and all-desktop windows remain untouched.
- [ ] 4.3 Restart the Debug application using the repository-required Debug restart sequence and verify the new process is running; verify a still-open matching window remains locked after restart.
