# Flowly (PowerShell module)

A thin C# (binary) PowerShell client for the [Flowly Management API](https://rasmustherkelsen.github.io/flowly/). Requires PowerShell 7.4+.

The module version is the release version: it equals the version of the `Flowly.ManagementApi` package published from the same release, and a prerelease release (for example `1.5.0-rc1`) is marked with the manifest's prerelease label (`rc1`).

```powershell
Connect-Flowly -BaseUri https://myapp.example.com/flowly/management -ApiKey $env:FLOWLY_API_KEY
# or, for Entra ID client-credentials:
Connect-Flowly -BaseUri https://myapp.example.com/flowly/management -TenantId $tenantId -ClientId $clientId -ClientSecret $clientSecret -Scope "api://flowly-management/.default"

Get-FlowlyMessageStreamCheckpoint
Get-FlowlyMessageStreamCheckpoint -QueueName my-queue -ConsumerName MyHandler

Reset-FlowlyMessageStreamCheckpoint -QueueName my-queue -ConsumerName MyHandler -Offset 42

Disconnect-Flowly
```

`Reset-FlowlyMessageStreamCheckpoint` supports `-WhatIf`/`-Confirm` (PowerShell's standard confirmation gate for destructive actions) since resetting a position causes reprocessing of any messages after the new offset once the handler restarts.

One cmdlet mirrors one Flowly Management API action — as the API grows, this module gains one cmdlet per new action, per `.claude/rules/management-api-conventions.md`.
