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
$apiKey = Read-Host -Prompt "Flowly API key" -AsSecureString
Connect-Flowly -BaseUri https://myapp.example.com/flowly/management -ApiKey $apiKey
# or from an environment variable:
Connect-Flowly -BaseUri https://myapp.example.com/flowly/management -ApiKey (ConvertTo-SecureString $env:FLOWLY_API_KEY -AsPlainText -Force)
# or, for Entra ID client-credentials:
$clientSecret = Read-Host -Prompt "Client secret" -AsSecureString
Connect-Flowly -BaseUri https://myapp.example.com/flowly/management -TenantId $tenantId -ClientId $clientId -ClientSecret $clientSecret -Scope "api://flowly-management/.default"
# or, for another identity provider, with its complete token endpoint (no -TenantId):
Connect-Flowly -BaseUri https://myapp.example.com/flowly/management -TokenEndpoint https://idp.example.com/connect/token -ClientId $clientId -ClientSecret $clientSecret -Scope "flowly"

Get-FlowlyMessageStreamCheckpoint
Get-FlowlyMessageStreamCheckpoint -QueueName my-queue -ConsumerName MyHandler

Reset-FlowlyMessageStreamCheckpoint -QueueName my-queue -ConsumerName MyHandler -Offset 42

Get-FlowlyMessageStreamPartition
Get-FlowlyMessageStreamPartition -State Halted
Resume-FlowlyMessageStreamPartition -QueueName my-queue -ConsumerName MyHandler -Partition 1
Get-FlowlyMessageStreamPartition -State Halted | Resume-FlowlyMessageStreamPartition

Get-FlowlyDeadLetter -Status Pending -QueueName my-queue
Get-FlowlyDeadLetter -MessageId $id | Submit-FlowlyDeadLetter -RequeuedBy "ops@example.com"
Submit-FlowlyDeadLetter -MessageId $id -MessageBody '{"orderId":42,"amount":12.5}'
Get-FlowlyDeadLetter -Status Pending -First 20 | Remove-FlowlyDeadLetter

Disconnect-Flowly
```

`-ApiKey` and `-ClientSecret` of `Connect-Flowly` are `SecureString` parameters; a plain string is rejected, and an empty value fails the connect. The module keeps a read-only copy of each as a `SecureString` and reveals it as text only while the API key header or the token request is built; it never logs either. Create one with `Read-Host -AsSecureString` or `ConvertTo-SecureString -AsPlainText -Force`.

`Connect-Flowly -Authority` sets the base URL the OAuth2 token is requested from (default `https://login.microsoftonline.com`, for a sovereign cloud use for example `https://login.microsoftonline.us`); the token endpoint is `{Authority}/{TenantId}/oauth2/v2.0/token`, so the authority must follow the Entra ID v2.0 layout. `-TokenEndpoint` replaces that whole URL for identity providers other than Entra ID; it is an alternative to `-TenantId` and `-Authority`, which cannot be combined with it, and `-TenantId` is neither required nor used with it. Both take an absolute https URL. A failed token request or API call fails with the error text the server sent.

`Reset-FlowlyMessageStreamCheckpoint` supports `-WhatIf`/`-Confirm` (PowerShell's standard confirmation gate for destructive actions) since resetting a position causes reprocessing of any messages after the new offset once the handler restarts.

`Get-FlowlyMessageStreamPartition` lists the state (`Running`, `Waiting`, `Halted`), the last error and the time of the last state change of the stream partitions owned by the instance the connection points at; `Resume-FlowlyMessageStreamPartition` re-runs the batch a `Halted` partition is holding (nothing is skipped; a batch that fails again halts the partition again) and supports `-WhatIf`/`-Confirm` with high impact. Both talk to one instance: with several instances, connect to the one that owns the partition. Resuming a partition that is not halted, or that the instance does not own, is an error carrying the API's explanation.

`Get-FlowlyDeadLetter` lists the dead letters of a host that registered dead letter tracking, newest first, with the filters `-QueueName` (the topic name for an event subscription dead letter), `-SubscriptionName`, `-Status` (`Pending` or `Requeued`) and `-MessageId`, and `-Skip`/`-First` (1 to 1000) for paging. Without `-First` the cmdlet returns every matching dead letter after `-Skip`: it reads the Management API in pages of 1000 until the list ends (the API itself answers a request without `take` with 100). With `-First` it sends one request and returns at most that many. `Submit-FlowlyDeadLetter` requeues a `Pending` dead letter to its original queue with a fresh set of retries (`-RequeuedBy` stores who asked; without it the API records the authenticated caller's identity: the principal's name for host authorization policies, the name, `sub` or `azp` claim for OAuth2, and nothing for an API key, which carries no identity) and `Remove-FlowlyDeadLetter` permanently discards one; both take `-MessageId` by property name, so they accept the output of `Get-FlowlyDeadLetter`, and both support `-WhatIf`/`-Confirm` with high impact. `Submit-FlowlyDeadLetter -MessageBody` republishes a corrected body instead of the stored one (the content type and properties stay those of the stored message): the stored body is kept in the record, and `Get-FlowlyDeadLetter` then shows `BodyReplaced` and the `ReplacementMessageBody` that was sent next to the unchanged `MessageBody`. `-MessageBody` is never bound from the pipeline, so a piped dead letter is requeued as it is. A replacement must not be empty or whitespace, must not exceed 256 KiB as UTF-8 and, when the stored body is valid JSON, must be valid JSON too (a stored body that is not JSON only has the empty and size checks applied). The API answers a rejected body with `400` and the cmdlet fails with its explanation. Requeuing or discarding a dead letter that is already requeued, or that does not exist, is an error carrying the API's explanation.

One cmdlet mirrors one Flowly Management API action — as the API grows, this module gains one cmdlet per new action, per `.claude/rules/management-api-conventions.md`.
