# Management API Conventions

`Flowly.ManagementApi` is a general, growing operational HTTP surface for Flowly — not a one-off endpoint. `Flowly.PowerShell` is its thin client. Every other cross-cutting area in this repo that requires several files to move together in lockstep has its own dedicated rules file (`templates-conventions.md`, `template-otel-versions.md`, `nuget-package-descriptions.md`); this is that file for the Management API + PowerShell client.

## The core mandate

Every addition, change, or removal of an `IFlowlyManagementAction` (new action, changed route/request/response shape, changed required capability, or removal) must be mirrored in `Flowly.PowerShell` in the **same change** — a new action gets a new cmdlet; a changed shape updates the corresponding cmdlet's parameters/output; a removed action gets its cmdlet removed (see `.claude/rules/deadcode-removal.md`).

The task is not done until `Flowly.ManagementApi`'s action set and `Flowly.PowerShell`'s cmdlet set agree.

## The mirroring convention

One `IFlowlyManagementAction` maps to exactly one cmdlet:

- **Verb**: chosen from PowerShell's approved-verb list to match the action's semantics — `Get` for a `Read`-capability list/query action; `Reset`/`Set`/`New`/`Remove` (whichever is the closest approved verb to what the action does) for a `Write`-capability mutation.
- **Noun**: always `Flowly` + the domain concept (e.g. `FlowlyMessageStreamCheckpoint`), never a bare noun — avoids collisions with other PowerShell modules, mirrors `Get-AzVM`/`Get-MgUser`. `Connect-Flowly`/`Disconnect-Flowly` are the only exceptions, since "Flowly" is already the noun there.
- **Confirmation**: a `Write`-capability action's cmdlet always declares `SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High` and calls `ShouldProcess(...)` before acting. A `Read`-capability action's cmdlet never does.
- **Implementation**: the cmdlet class stays a thin `PSCmdlet` wrapper — HTTP-calling logic lives in `Flowly.PowerShell.Http.FlowlyApiClient`, so it stays unit-testable without a PowerShell host.

## Files to keep in sync

| Change | Update |
|---|---|
| New/changed `IFlowlyManagementAction` implementation | `src/Flowly.ManagementApi/**` (the action itself) |
| Matching cmdlet | `src/Flowly.PowerShell/Cmdlets/*.cs`, plus the `CmdletsToExport` list in `src/Flowly.PowerShell/Flowly.psd1` |
| Usage docs | `src/Flowly.ManagementApi/README.md`, `src/Flowly.PowerShell/README.md`, `README.md` (root), `docs/ai/CONTEXT.md` |

This table itself must be extended whenever a new feature package starts contributing actions (e.g. Jobs or DeadLetters actions land here later) — the same self-maintaining instruction `template-otel-versions.md` carries for its own file list.

## Version alignment

Bump the published `Flowly.PowerShell` module version (`ModuleVersion` in `Flowly.psd1`) and `Flowly.ManagementApi`'s package version together whenever the action set changes, so a mismatch between them is visible at a glance in the PowerShell Gallery / NuGet listings. The exact scheme (lockstep version numbers vs. independent semver with a documented compatibility table) is an implementation-time decision — but *some* explicit alignment signal is required, not silence.
