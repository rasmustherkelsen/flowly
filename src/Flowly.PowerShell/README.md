# Flowly.PowerShell (PowerShell module)

A thin C# (binary) PowerShell client for the [Flowly Management API](https://rasmustherkelsen.github.io/flowly/). Requires PowerShell 7.4+.

The module version is the release version: it equals the version of the `Flowly.ManagementApi` package published from the same release, and a prerelease release (for example `1.5.0-rc1`) is marked with the manifest's prerelease label (`rc1`).

## Install

The module is published to nuget.org as the `Flowly.PowerShell` package, not to the PowerShell Gallery. [PSResourceGet](https://learn.microsoft.com/powershell/gallery/powershellget/supported-repositories) supports nuget.org as a repository and ships with PowerShell 7.4 and later (the module requires 7.4 or later as well). Register nuget.org once, then install and import by the package id:

```powershell
Register-PSResourceRepository -Name NuGetGallery -Uri https://api.nuget.org/v3/index.json
Install-PSResource Flowly.PowerShell -Repository NuGetGallery -TrustRepository
Import-Module Flowly.PowerShell
```

nuget.org is not a trusted repository by default, so `Install-PSResource` asks for confirmation unless `-TrustRepository` is passed. Add `-Prerelease` to install a prerelease version, or `-Version` for a specific one. nuget.org does not support wildcard search or search by command name from PSResourceGet, so install by the exact package id.

## Usage

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
