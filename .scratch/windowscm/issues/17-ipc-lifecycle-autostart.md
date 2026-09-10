# 17: IPC, lifecycle and autostart

**What to build:** a well-behaved Windows citizen: single instance with
handoff, scriptable control over pipe and CLI, opt-in autostart, and
sub-second session-end cleanup that never blocks logout.

**Blocked by:** 09 (history store).

**Status:** resolved

- [x] Second launch forwards to the running instance and exits (named mutex plus pipe handoff)
- [x] Pipe protocol covers toggle/show/hide/clear/clear-all with current-user-only access
- [x] CLI mirrors the protocol including clear variants and hidden-start flag
- [x] Autostart is opt-in (installer checkbox plus settings toggle) via the Run key
- [x] Session-end cleanup finishes fast, never cancels logout, and unsubscribes cleanly

## Comments

Implemented 2026-09-10 via TDD (red-green per seam) + two-axis self-review
(Standards + Spec). 649/649 xUnit green (562 prior + 87 new), 0 errors,
0 warnings (.NET 8.0.425, no new packages — BCL + Microsoft.Data.Sqlite only).
Seams in `Lifecycle/`: `IpcCommand` (Toggle/Show/Hide/Clear/ClearAll + Ping
health-check extra) + `IpcProtocol` (lowercase line framing, case-insensitive,
underscore alias, unknown→false never throws), `InstanceNames`
(`Local\WindowsCM.<sid>` mutex + `WindowsCM.<sid>` pipe, SID validation,
per-test pipe names), `CliOptions` (exe-path skipping, `--`/`-`/`/` prefixes,
first-command-wins, `--hidden` composes, no-arg forward is toggle),
`IpcDispatcher` over `ITrayPopup` + real `:memory:` store (Clear keeps
protected, ClearAll wipes, Ping side-effect-free, unknown→"unknown"),
`SingleInstanceCoordinator` over `ISingleInstanceLock`/`IIpcForwarder`
(primary/forwarded/failed, 2s default, never a second UI on failure),
`AutostartManager` over `IRunKeyStore` (`"<exe>" --hidden`, idempotent
disable) + `RegistryRunKeyStore` via Advapi32 P/Invoke (read-null-when-absent,
Windows-only guard), `SessionJanitor` over `ISessionEndingSource` (live mode
provider, sync <1s `SessionCleanup.Apply`, Cancel never set, failures
swallowed, Dispose unsubscribes), `Win32/` production
`MutexSingleInstanceLock` (initial-ownership + abandoned-stale recovery),
`NamedPipeServer`/`NamedPipeForwarder` (BOM-less UTF-8 lines, sequential
single-instance accept, per-connection error containment).
Review fixes folded in: BOM-preamble pipe stall found via minimal repro
(`Encoding.UTF8`→`UTF8Encoding(false)`, proven raw-vs-SRW), sequential accept
(max-1 second-instance fault), stale class comment, dead CancellationToken
param, fault-tolerant StopAsync, disposed-Start guard, abandoned-null honesty.
Deferred by ticket boundary: real WPF startup wiring (primary/secondary
decide, server lifetime, `SystemEvents.SessionEnding` adapter — needs a pump
per research 05 §4, package stays out of Core), tray-exit disposal chain,
settings-toggle Run-key binding + installer checkbox (18); known
`CurrentUserOnly` elevation split (elevated/non-elevated instances do not see
each other — same-user same-elevation by design, manual smoke only for real
HKCU writes, elevated-target paste, and multi-session mutex isolation).

Formal /code-review 2026-09-10 (diff 54c5686..5cc62b9): Standards 0 hard + 5 smells (kept); Spec 9 findings. Fixed: bare --hidden second launch forwards nothing (quiet Forwarded exit, UI never pops on login); pipe handler failures reply "error" (IpcProtocol.Error; sender distinguishes failed clear). Accepted: ping + CLI leniency extras. Open: autostart checkbox/toggle + WPF startup wiring + timed cleanup (shell/18), IsEnabled over-match, Local\ per-session mutex semantics. Suite 689/689. Status → resolved.
