# Flowly.ManagementApi

An opt-in, machine-oriented HTTP surface for operational actions against a running [Flowly](https://rasmustherkelsen.github.io/flowly/) host — no UI, independent of `Flowly.Dashboard`. This is the start of a general Flowly management surface: message stream checkpoint list/reset, message stream partition list/resume and dead letter list/requeue/discard are its capabilities, and future features register additional `IFlowlyManagementAction` implementations into the same API.

## Quick start

```csharp
builder.Services.AddFlowlyManagementApi(options =>
{
    options.BasePath = "/flowly/management";   // default, configurable
    options.RequireApiKey(apiKey);              // or options.UseOAuth2(...), or options.RequireAuthorizationPolicies(...)
});
```
```csharp
app.UseFlowlyManagementApi();
```

Call `UseFlowlyManagementApi()` after every `Add<Provider>MessageStreamPersistence()` call — it feature-detects registered checkpoint persistence stores at that point and only maps the message stream checkpoint actions when one exists. The stream partition actions are mapped when the host registered Flowly's stream partition state and resume services, which `AddFlowly` does (an instance without stream handlers simply lists nothing). The dead letter actions are mapped when the host registered dead letter tracking (`Add<Provider>DeadLetterTracking` or `Add<Provider>DeadLetterTrackingClient`, which register `IDeadLetterService`); without it the dead letter routes do not exist.

## Message stream actions

| Action | Route | Authorization | PowerShell cmdlet |
|---|---|---|---|
| List checkpoint positions | `GET {BasePath}/message-streams/checkpoints?queue=&consumer=&store=` | Read | `Get-FlowlyMessageStreamCheckpoint` |
| Reset a checkpoint position | `POST {BasePath}/message-streams/checkpoints/reset` | Write | `Reset-FlowlyMessageStreamCheckpoint` |
| List the stream partitions of this instance | `GET {BasePath}/message-streams/partitions?queue=&consumer=&partition=&state=` | Read | `Get-FlowlyMessageStreamPartition` |
| Resume a halted stream partition | `POST {BasePath}/message-streams/partitions/resume` | Write | `Resume-FlowlyMessageStreamPartition` |

### Stream partitions

The partition actions are runtime controls on the live consumers of the instance that answers, not checkpoint store actions: each instance lists and resumes only the partitions it owns, nothing is signalled between instances, and a partition is never handed to another instance on purpose. With several instances, address the one that owns the partition.

`GET .../partitions` returns an array of `{ handlerName, queueName, consumerName, partition, state, stateChangedAtUtc, lastError }`, where `state` is `Running`, `Waiting` (start position unresolved, no processing loop yet) or `Halted` (in-process retries exhausted), `partition` is `null` for a non-partitioned stream and `lastError` is `{ message, type, occurredAtUtc }` or `null`. The optional filters `queue`, `consumer`, `partition` and `state` (case-insensitive; `400` for an unknown value) narrow it down.

`POST .../partitions/resume` takes `{ "queueName": "...", "consumerName": "...", "partition": 0 }` (omit `partition` or send `null` for a non-partitioned stream). It re-runs the batch the halted partition is holding with a fresh set of in-process retries and lets the partition continue from it. Nothing is skipped: when the batch fails again the partition halts again and can be resumed again. Responses, with problem details bodies for the errors: `204` resumed, `400` blank queue or consumer, `404` this instance does not own the partition, `409` the partition is not `Halted` (a `Waiting` partition is retried automatically, there is nothing to resume).

## Dead letter actions

| Action | Route | Authorization | PowerShell cmdlet |
|---|---|---|---|
| List dead letters | `GET {BasePath}/dead-letters?queue=&subscription=&status=&messageId=&skip=&take=` | Read | `Get-FlowlyDeadLetter` |
| Requeue a dead letter | `POST {BasePath}/dead-letters/requeue` | Write | `Submit-FlowlyDeadLetter` |
| Discard a dead letter | `POST {BasePath}/dead-letters/discard` | Write | `Remove-FlowlyDeadLetter` |

The actions call the same `IDeadLetterService` the Dashboard uses, so they list, requeue and discard the records of the dead letter tracking database, and any instance of the host can answer. Add the `Flowly.DeadLetters` backend to the host as usual (`AddSqlServerDeadLetterTracking`, `AddPostgresDeadLetterTracking`, `AddSQLiteDeadLetterTracking`, or the read-only `Add<Provider>DeadLetterTrackingClient` for a process that only operates on dead letters).

`GET .../dead-letters` returns an array of `{ messageId, queueName, subscriptionName, messageBody, messageProperties, deadLetteredAtUtc, deadLetterReason, deadLetterErrorDescription, status, requeuedAtUtc, requeuedBy }`, newest first (`messageId` breaks ties), where `status` is `Pending` or `Requeued` and `subscriptionName` is `null` for a queue dead letter (for an event subscription dead letter `queueName` holds the topic name). The optional filters `queue`, `subscription`, `status` (case-insensitive) and `messageId` narrow it down, and `skip` (zero or greater) with `take` (1 to 1000) page through it; without `take` everything after `skip` is returned. An unknown `status` or an out-of-range `skip` or `take` is `400`.

`POST .../dead-letters/requeue` takes `{ "messageId": "...", "requeuedBy": "..." }` (`requeuedBy` is optional and stored as the audit trail). The message is sent back to its original queue, or, for an event subscription dead letter, to the topic addressed to the originating subscriber only, with the Flowly retry count cleared, and the dead letter becomes `Requeued`. `POST .../dead-letters/discard` takes `{ "messageId": "..." }` and permanently deletes the record without sending the message anywhere. The message id travels in the body because broker-assigned ids are free-form strings. Responses, with problem details bodies for the errors: `204` done, `400` blank message id, `404` no dead letter has the id, `409` the dead letter is not `Pending` (a requeued dead letter can be neither requeued nor discarded again). A broker failure while requeuing surfaces as the host's standard `500` and leaves the dead letter `Pending`.

## Authorization

Exactly one authorization mode must be configured, or `UseFlowlyManagementApi()` throws at startup:

- **API key** — `options.RequireApiKey(apiKey)` (single key, both read and write) or `options.RequireApiKey(readApiKey, writeApiKey)` (read/write split). Presented via the `X-Flowly-Api-Key` request header.
- **OAuth2** — `options.UseOAuth2(oauth => { oauth.Authority = "..."; oauth.Audience = "..."; })`, for protecting the API with an Entra ID (or any OIDC) app registration, including the client-credentials flow. The `ReadRequirement`/`WriteRequirement` claim values are checked against both the `roles` claim (Entra App Roles — what an app-only, client-credentials token carries) and the `scp` claim (delegated OAuth scopes — what an interactive sign-in token carries).
- **Existing policies** — `options.RequireAuthorizationPolicies(readPolicyName, writePolicyName)` defers entirely to authorization policies the host already configured.

## Discovery

`UseFlowlyManagementApi()` maps a standard OpenAPI document at `{BasePath}/openapi.json` (protected by the Read policy by default — set `options.ProtectDiscoveryDocument = false` to make it public). Set `options.EnableDocsUi = true` to also serve a minimal interactive explorer at `{BasePath}/docs`.

## Extensibility

Register `IFlowlyManagementAction` implementations into DI to contribute additional actions — `UseFlowlyManagementApi()` discovers and maps every one, applying the authorization policy matching its declared `RequiredCapability`.
