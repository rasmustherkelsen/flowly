<img src="docs/assets/flowly-logo.svg" alt="Flowly" height="56">

Flowly is a queue-based messaging abstraction for .NET. It sits between your application code and the underlying message broker, giving you a clean, convention-driven API for message handling, job tracking, retries, dead letter management, and recurring scheduled work.

[![Build](https://github.com/rasmustherkelsen/flowly/actions/workflows/build.yml/badge.svg)](https://github.com/rasmustherkelsen/flowly/actions/workflows/build.yml)

---

## Dashboard

Flowly ships an embedded management dashboard at `/flowly` — inspect job history, browse dead letters, trigger recurring jobs, and submit messages directly from the browser. OAuth2/OIDC authentication (Azure Entra ID, Google, or any OpenID Connect provider) is opt-in via `AddFlowlyDashboard(options => options.Authentication = new OAuthAuthenticationOptions(...))`, with optional role and policy restrictions to separate read-only viewers from users who can submit messages. See the [Dashboard Authentication guide](docs/dashboard-authentication.md) for step-by-step Azure Entra ID and Google setup.

The dead letters page requeues or discards a message, and the requeue dialog shows the stored body in an editable text area: change it to republish a corrected body instead (it is sent only when changed, must be valid JSON when the stored body is JSON, and the original stays in the record), or leave it as it is to requeue the stored message. The detail view shows the replacement next to the original. Requeue and discard use the same role or policy as message submission. See [Fix and requeue a dead letter](#fix-and-requeue-a-dead-letter).

There is no fixed default port. When embedded in an existing app, the dashboard runs on whatever port that app's Kestrel/`launchSettings.json` already uses. When scaffolded standalone via `--dashboard` (see [Project Templates](#project-templates)), each `dotnet new` invocation gets its own randomly assigned port — HTTP 5000–5300 / HTTPS 7000–7300 for `flowlyapp`, HTTP 5400–5499 / HTTPS 7400–7499 for `flowlyaspireapp` — recorded in that project's `Properties/launchSettings.json`.

<img src="docs/assets/dashboard-screenshot-jobs.png" alt="Flowly Dashboard — Jobs" width="100%">
<img src="docs/assets/dashboard-screenshot-call.png" alt="Flowly Dashboard — Submit (Call)" width="100%">

---

## Management API

For operators who don't want to install the Dashboard just to reset a stuck message stream, `Flowly.ManagementApi` is a separate, opt-in, machine-oriented HTTP surface — no UI, independent of `Flowly.Dashboard`. Message stream checkpoint list/reset, message stream partition list/resume (see [Operating halted partitions](#operating-halted-partitions-management-api-and-powershell)) and dead letter list/requeue/discard (see [Operating dead letters](#operating-dead-letters-management-api-and-powershell)) are its capabilities, exposed via a standard OpenAPI-documented endpoint and a companion `Flowly.PowerShell` module:

```csharp
builder.Services.AddFlowlyManagementApi(options => options.RequireApiKey(apiKey)); // or UseOAuth2(...) for Entra ID
```
```csharp
app.UseFlowlyManagementApi();
```

See [`src/Flowly.ManagementApi/README.md`](src/Flowly.ManagementApi/README.md) for authorization modes (API key, OAuth2/Entra ID client-credentials, or existing host policies) and the `IFlowlyManagementAction` extensibility point future features plug into, and [`src/Flowly.PowerShell/README.md`](src/Flowly.PowerShell/README.md) for the `Connect-Flowly`/`Get-FlowlyMessageStreamCheckpoint`/`Reset-FlowlyMessageStreamCheckpoint`/`Get-FlowlyMessageStreamPartition`/`Resume-FlowlyMessageStreamPartition`/`Get-FlowlyDeadLetter`/`Submit-FlowlyDeadLetter`/`Remove-FlowlyDeadLetter` cmdlets.

---

## Quick Navigation

- [RabbitMQ Quickstart](docs/quickstart-rabbitmq.md)
- [Azure Service Bus Quickstart](docs/quickstart-azure-service-bus.md)
- [InMemory Quickstart](docs/quickstart-inmemory.md)
- [Management API](#management-api)
- [Why Flowly?](#why-flowly)
- [Packages](#packages)
- [Installation](#installation)
- [Getting Started](#getting-started)
- [Defining Messages](#defining-messages)
- [Message Handlers](#message-handlers)
- [Sending Messages](#sending-messages)
- [Message Streaming](#message-streaming)
- [Events (Fan-Out)](#events-fan-out)
- [Handler steps and send steps](#handler-steps-and-send-steps)
- [Topology Name Resolution](#topology-name-resolution)
- [Retry Policy](#retry-policy)
- [Dead Letter Tracking](#dead-letter-tracking)
- [Job Tracking](#job-tracking)
- [Recurring Jobs](#recurring-jobs)
- [Local Development](#local-development)
- [Flowly.Tool CLI](#flowlytool-cli)
- [Project Templates](#project-templates)
- [Full Configuration Example](#full-configuration-example)
- [Multi-Provider](#multi-provider)
- [Azure Service Bus Transport](#azure-service-bus-transport)
- [RabbitMQ Transport](#rabbitmq-transport)
- [Azure Event Hubs Transport](#azure-event-hubs-transport)
- [In-Memory Transport](#in-memory-transport)
- [OpenTelemetry](#opentelemetry)
- [Samples](#samples)
- [Claude Code Skills](#claude-code-skills)
- [Dashboard Authentication](docs/dashboard-authentication.md)
- [Attributes Reference](docs/attributes-reference.md)
- [Contributing](#contributing)
- [Repository](#repository)
- [Status](#status)

---

## Why Flowly?

- **Provider-agnostic** — swap the message broker without changing application code
- **Convention-driven** — queue names derived automatically from message types; minimal boilerplate
- **Job tracking built-in** — first-class support for tracking long-running job state in SQL Server, PostgreSQL, or SQLite
- **Retry and dead letter handling** — configurable retry with delay, and a persistent dead letter store
- **Recurring jobs** — CRON-based scheduling with guaranteed single-execution semantics
- **Handler steps and send steps** — cross-cutting behavior such as audit, logging scope, idempotency or authorization around handlers and before sends, selected by message type
- **Local development first** — tooling for emulator configs, .NET Aspire integration, and Docker Compose

---

## Packages

All packages are published to [NuGet.org](https://www.nuget.org/packages?q=Flowly).

| Package | Description |
|---|---|
| `Flowly` | Core abstractions: handlers, senders, queue topology, retry engine |
| `Flowly.AzureServiceBus` | Azure Service Bus transport |
| `Flowly.AzureServiceBus.Aspire` | .NET Aspire AppHost integration — automatically discovers and registers queue topology from a service project's `FlowlyConfiguration` for the Azure Service Bus emulator |
| `Flowly.RabbitMQ` | RabbitMQ transport |
| `Flowly.AzureEventHubs` | Azure Event Hubs stream-only transport — `IMessageRecorder` and `MessageStreamHandler<T>` (including `[StreamPartitions]`, start positions and checkpoints) against an event hub validated at startup; no queues, events, jobs or dead letters |
| `Flowly.InMemory` | In-memory transport — no broker required; ideal for testing and local development |
| `Flowly.Jobs` | Job state tracking and CRON scheduling core |
| `Flowly.Jobs.SqlServer` | SQL Server backend for job state tracking |
| `Flowly.Jobs.Postgres` | PostgreSQL backend for job state tracking |
| `Flowly.Jobs.SQLite` | SQLite backend for job state tracking |
| `Flowly.DeadLetters` | Dead letter tracking core |
| `Flowly.DeadLetters.SqlServer` | SQL Server backend for dead letter tracking |
| `Flowly.DeadLetters.Postgres` | PostgreSQL backend for dead letter tracking |
| `Flowly.DeadLetters.SQLite` | SQLite backend for dead letter tracking |
| `Flowly.MessageStreams` | Built-in position persistence for message stream checkpoints core |
| `Flowly.MessageStreams.SqlServer` | SQL Server backend for message stream checkpoint persistence |
| `Flowly.MessageStreams.Postgres` | PostgreSQL backend for message stream checkpoint persistence |
| `Flowly.MessageStreams.SQLite` | SQLite backend for message stream checkpoint persistence |
| `Flowly.OpenTelemetry` | OpenTelemetry metrics and traces for handlers and submitters |
| `Flowly.Dashboard` | Embedded web dashboard middleware — submit messages, browse jobs, inspect dead letters, and trigger recurring jobs at `/flowly`; opt-in OAuth2/OIDC authentication with role and policy authorization |
| `Flowly.ManagementApi` | Opt-in, machine-oriented HTTP surface for operational actions (message stream checkpoint list/reset, stream partition list/resume and dead letter list/requeue/discard) — independent of `Flowly.Dashboard` |
| `Flowly.PowerShell` | C# PowerShell module client for the Management API (`Connect-Flowly`, `Get-FlowlyMessageStreamCheckpoint`, `Reset-FlowlyMessageStreamCheckpoint`, `Get-FlowlyMessageStreamPartition`, `Resume-FlowlyMessageStreamPartition`, `Get-FlowlyDeadLetter`, `Submit-FlowlyDeadLetter`, `Remove-FlowlyDeadLetter`) |
| `Flowly.Tool` | `flowly` CLI for queue discovery, code generation, and message stream checkpoint operations |
| `Flowly.Templates` | `dotnet new flowlyapp` / `dotnet new flowlyaspireapp` / `dotnet new flowly` project templates |

---

## Installation

Install the transport package for your broker — it pulls in `Flowly` core automatically:

```bash
# Azure Service Bus
dotnet add package Flowly.AzureServiceBus

# RabbitMQ
dotnet add package Flowly.RabbitMQ

# In-memory (no broker — testing / local dev)
dotnet add package Flowly.InMemory

# Azure Event Hubs (stream-only — add it next to a queue transport)
dotnet add package Flowly.AzureEventHubs
```

Add optional feature packages as needed:

```bash
# Job state tracking — pick the backend that matches your database
dotnet add package Flowly.Jobs.SqlServer
dotnet add package Flowly.Jobs.Postgres
dotnet add package Flowly.Jobs.SQLite

# Dead letter tracking — pick the backend that matches your database
dotnet add package Flowly.DeadLetters.SqlServer
dotnet add package Flowly.DeadLetters.Postgres
dotnet add package Flowly.DeadLetters.SQLite

# Message stream checkpoint persistence — pick the backend that matches your database
dotnet add package Flowly.MessageStreams.SqlServer
dotnet add package Flowly.MessageStreams.Postgres
dotnet add package Flowly.MessageStreams.SQLite

# Management API — opt-in operational HTTP surface (independent of Flowly.Dashboard)
dotnet add package Flowly.ManagementApi

# OpenTelemetry instrumentation
dotnet add package Flowly.OpenTelemetry
```

Install the `Flowly.PowerShell` module (a thin client for the Management API) from nuget.org with PSResourceGet, which ships with PowerShell 7.4 and later. Register nuget.org as a repository once, then install and import:

```powershell
Register-PSResourceRepository -Name NuGetGallery -Uri https://api.nuget.org/v3/index.json
Install-PSResource Flowly.PowerShell -Repository NuGetGallery -TrustRepository
Import-Module Flowly.PowerShell
```

Add `-Prerelease` to `Install-PSResource` for a prerelease version. The module version equals the Flowly release version. See [`src/Flowly.PowerShell/README.md`](src/Flowly.PowerShell/README.md) for details.

Install the `flowly` CLI tool globally for local development and code generation:

```bash
dotnet tool install --global Flowly.Tool
```

Install the project templates to scaffold new Flowly services with `dotnet new`:

```bash
dotnet new install Flowly.Templates
```

---

## Getting Started

### 1. Create a configuration class

Every deployable service has exactly one configuration class that inherits `Configuration`. This is where you wire up the transport, handlers, and optional features.

```csharp
using Flowly.AzureServiceBus;

public class MyServiceConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("AzureServiceBus")   // connection string name in appsettings
            .AddMessageHandler<OrderCreated, OrderCreatedHandler>();
    }
}
```

### 2. Register in Program.cs

```csharp
builder.AddFlowly<MyServiceConfiguration>();
```

Flowly registers all background services, queue topology, and DI bindings automatically.

---

## Defining Messages

Messages are plain C# records or classes — no base type required for regular messages.

```csharp
// Queue name auto-generated: "order-created"
public record OrderCreated(Guid OrderId, decimal Total);

// Explicit queue name
[QueueName("orders-v2")]
public record OrderCreated(Guid OrderId, decimal Total);
```

### Queue name auto-generation

Flowly derives the queue name from the message type name using `KebabCaseTopologyNameResolver` (the built-in default): PascalCase is split on capital letters, joined with `-`, lowercased, and a trailing `Message` suffix is stripped.

| Type name | Queue name |
|---|---|
| `OrderCreated` | `order-created` |
| `ProcessOrderMessage` | `process-order` |
| `RebuildSearchIndexMessage` | `rebuild-search-index` |

Only add `[QueueName]` when the auto-generated name is wrong. See [Topology Name Resolution](#topology-name-resolution) to replace the naming strategy entirely.

---

## Message Handlers

### Regular handler

Processes one message at a time. Throw to reject the message; return to acknowledge it.

```csharp
public class OrderCreatedHandler : MessageHandler<OrderCreated>
{
    public override async Task Handle(IMessageContext<OrderCreated> ctx)
    {
        var order = ctx.Message;
        var ct    = ctx.CancellationToken;

        await ProcessOrder(order, ct);
    }
}
```

Register:

```csharp
builder.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
```

### Batch handler

Receives multiple messages in a single call. Useful for bulk inserts or aggregations.

```csharp
[BatchProcessing(maxMessages: 100, maxWaitTimeInSeconds: 30)]
public class EventBatchHandler : BatchMessageHandler<AnalyticsEvent>
{
    public override async Task Handle(IBatchMessageContext<AnalyticsEvent> ctx)
    {
        await BulkInsert(ctx.Messages, ctx.CancellationToken);
    }
}
```

Register:

```csharp
builder.AddBatchMessageHandler<AnalyticsEvent, EventBatchHandler>();
```

**Delivery semantics:** by default, batch handlers use **at-most-once** delivery — messages are acknowledged before `Handle` is called. If the handler throws, those messages are gone and will not be redelivered. This suits bulk-write workloads where a duplicate insert is worse than a dropped message.

To opt in to **at-least-once** delivery with automatic retry, apply `[RetryPolicy]`:

```csharp
[BatchProcessing(maxMessages: 100, maxWaitTimeInSeconds: 30)]
[RetryPolicy(maxRetries: 3, delaySeconds: 30)]
public class EventBatchHandler : BatchMessageHandler<AnalyticsEvent>
{
    ...
}
```

When `[RetryPolicy]` is configured and `Handle` throws, Flowly republishes the **entire batch** to the same queue with an incremented retry counter, then acknowledges the originals. After all retries are exhausted the batch is discarded (batch handlers do not support dead letter tracking).

> **Important:** when `[RetryPolicy]` is used on a batch handler, the handler **must be idempotent** — the whole batch is redelivered on failure, so processing the same message twice must produce the same outcome.

### Queue configuration attributes

These attributes go on the **handler** class:

| Attribute | Purpose | Default |
|---|---|---|
| `[DefaultMessageTimeToLive("1.00:00:00")]` | How long a message lives in the queue | 1 day |
| `[LockDuration("00:05:00")]` | How long the message is locked during processing | 5 minutes |
| `[DeadLetterOnMessageExpiration(true)]` | Dead-letter messages that exceed TTL | `true` |
| `[RetryPolicy(maxRetries, delaySeconds)]` | Retry on handler failure | 0 retries |
| `[MaxConcurrentCalls(n)]` | Number of messages processed in parallel | 1 |

See the [Attributes Reference](docs/attributes-reference.md) for every attribute — handler and message/event contract alike — in one place.

Or override `Configure` on the handler:

```csharp
public class OrderCreatedHandler : MessageHandler<OrderCreated>
{
    public override void Configure(HandlerQueueOptions options)
    {
        options.MaxConcurrentCalls = 5;
        options.LockDuration = TimeSpan.FromMinutes(10);
        options.MaxRetries = 3;
        options.RetryDelaySeconds = 60;
    }

    public override Task Handle(IMessageContext<OrderCreated> ctx) => ...
}
```

---

## Sending Messages

Add a submitter registration for any queue you want to send to:

```csharp
builder.AddMessageSubmitter<OrderCreated>();
```

Then inject `IMessageSender` and call `Send`:

```csharp
public class OrderService(IMessageSender sender)
{
    public async Task PlaceOrder(Order order)
    {
        await sender.Send(new OrderCreated(order.Id, order.Total));
    }
}
```

### Custom message properties

A message can carry application-defined string key/value pairs next to its body, for example the tenant or the id of a business operation, without every contract declaring those fields. Pass them in the optional `customProperties` parameter, which `Send`, `Record`, `RaiseEvent`, `Call` and `QueueJob` all take as their last parameter:

```csharp
await sender.Send(
    new OrderCreated(order.Id, order.Total),
    customProperties: new Dictionary<string, string> { ["tenant"] = "contoso" });
```

The receiver reads them from `MessageProperties.CustomProperties`, which is never `null` (empty when the message carries none). `IMessageContext<T>` (message, call and job handlers), `IEventContext<T>` and a handler step's context expose a `Properties` value; `IBatchMessageContext<T>` and `IMessageStreamContext<T>` expose `Properties` as a list with one entry per message, in the order of `Messages`:

```csharp
public class OrderCreatedHandler : MessageHandler<OrderCreated>
{
    public override Task Handle(IMessageContext<OrderCreated> context)
    {
        var tenant = context.Properties.CustomProperties.GetValueOrDefault("tenant");
        ...
    }
}
```

- **Strings only.** The value type every transport round-trips unchanged: RabbitMQ headers, Azure Service Bus application properties, Azure Event Hubs event properties and the InMemory envelope. Serialize anything richer yourself.
- **Reserved keys.** Keys starting with `flowly-` (case-insensitive), `traceparent` and `tracestate` belong to Flowly and the W3C trace context. Passing one, an empty key or a `null` value throws `ArgumentException` at the call site before anything is sent, and reserved keys never show up in `CustomProperties` on a received message. Other keys, including `x-` keys, are carried as they are; on RabbitMQ the broker-set `x-first-death-*` and `x-last-death-*` headers are not part of the bag.
- **Preserved by retries and requeues.** A retry re-publishes the message with its properties, and a dead letter requeue re-sends the stored raw application properties, so the pairs follow the message. A call handler's reply is a new message and does not carry the request's properties.
- **Streams.** Stream records carry the properties on RabbitMQ (streams and Super Streams), Azure Event Hubs and InMemory, and a stream handler reads them per message from `context.Properties[i].CustomProperties`.
- **Not authenticated.** Anyone who can publish to the queue can set any value, so the properties are not proof of identity. Sign the value yourself if a receiver has to trust it, and rely on the broker's access control as the real boundary.

---

## RPC-Style Calls

`CallHandler` lets you make blocking remote-procedure-call style requests: the caller sends a message and waits for a typed response before continuing.

### Message contract

The call message implements `IReturns<TReturn>` to declare its response type:

```csharp
public record ReturnMessage(string ReturnValue);

public record CallMessage(string Payload) : IReturns<ReturnMessage>;
```

### Handler (receiver side)

```csharp
public class CallMessageHandler : CallHandler<CallMessage, ReturnMessage>
{
    protected override Task<ReturnMessage> Handle(IMessageContext<CallMessage> ctx)
        => Task.FromResult(new ReturnMessage($"Received: {ctx.Message.Payload}"));
}
```

Register in the receiver's configuration:

```csharp
builder.AddCallHandler<CallMessage, CallMessageHandler>();
```

The `CallHandler` supports the same attributes and `Configure` overrides as `MessageHandler` (retry policy, queue name, concurrency, etc.).

### Caller (sender side)

Set `InstanceName` in options and register a call submitter:

```csharp
builder.AddFlowly<FlowlyConfiguration>(options =>
{
    options.InstanceName = "my-service";  // required — used for the reply queue name
});

// In FlowlyConfiguration.Configure:
builder.AddCallSubmitter<CallMessage>();

// Optional per-submitter timeout (defaults to FlowlyOptions.MessageCallTimeout = 2 min):
builder.AddCallSubmitter<CallMessage>(opts => opts.Timeout = TimeSpan.FromSeconds(30));
```

Inject `IMessageCaller` and call:

```csharp
public class MyService(IMessageCaller caller, ILogger<MyService> logger)
{
    public async Task DoWork(CancellationToken ct)
    {
        ReturnMessage response = await caller.Call<CallMessage, ReturnMessage>(
            new CallMessage("hello"), ct);
        logger.LogInformation("{ReturnValue}", response.ReturnValue);
    }
}
```

### Infrastructure

Each sender gets a dedicated reply queue named `{callQueue}.reply.{instanceName}` (e.g. `call-message.reply.my-service`). Flowly creates this queue at startup, routes responses to it via `CorrelationId`, and resolves the waiting `Call` task. The sender also declares the call queue itself at its own startup, so on RabbitMQ a call made before the `CallHandler` process has ever started is queued for delivery rather than silently dropped — the queue exists regardless of which process starts first.

> **Note:** Attributes on the return message type (`[QueueName]`, `[RetryPolicy]`, `[ProviderAffinity]`, etc.) are **silently ignored** on the reply path. The response is delivered via the infrastructure reply queue and these attributes only apply if `ReturnMessage` is also independently registered as a normal handler or submitter elsewhere.

---

## Message Streaming

> **Stream handlers and `IMessageRecorder` run on RabbitMQ, InMemory and [Azure Event Hubs](#azure-event-hubs-transport).** RabbitMQ uses its native stream queue type (`x-queue-type: stream`); Azure Event Hubs uses an event hub, with the handler's `ConsumerName` as the consumer group; the InMemory transport backs streams with an in-process append-only log instead — no broker required, ideal for local development and testing (also a reasonable fit for small, single-instance deployments where avoiding external broker infrastructure is the point, e.g. a self-hosted app on a home server/NAS or a single container). Azure Service Bus has no equivalent primitive. Registering a stream handler or recorder against a non-stream-capable provider throws `InvalidOperationException` at startup, not at first use.

A message stream is an append-only, replayable log: unlike a regular queue, multiple independent consumers can each read the same stream from their own position — including consumers that didn't exist when a message was recorded. `IMessageRecorder.Record()` is a third sending verb alongside `IMessageSender.Send()` (fire-and-forget) and `IMessageCaller.Call()` (RPC). A stream can optionally be [partitioned](#partitioned-streams) into independent, ordered sub-logs for horizontal scale-out on RabbitMQ and Azure Event Hubs.

### Recording onto a stream

```csharp
public record TelemetryReading(string SensorId, double Value);

builder.AddMessageRecorder<TelemetryReading>();
```

```csharp
public class SensorService(IMessageRecorder messageRecorder)
{
    public Task Record(TelemetryReading reading, CancellationToken ct) => messageRecorder.Record(reading, ct);
}
```

### Consuming a stream

```csharp
public class TelemetryStreamHandler : MessageStreamHandler<TelemetryReading>
{
    public override void Configure(MessageStreamHandlerOptions options)
    {
        options.StartPosition = StartPosition.Last();   // required — no default (see below)
        options.MaxMessagesBeforeProcessing = 100;
        options.MaxWaitTime = TimeSpan.FromSeconds(30);
    }

    public override async Task Handle(IMessageStreamContext<TelemetryReading> ctx)
    {
        foreach (var reading in ctx.Messages)
            await Save(reading);
    }
}
```

Register:

```csharp
builder.AddMessageStreamHandler<TelemetryReading, TelemetryStreamHandler>();
```

> **Prefetch matches `MaxMessagesBeforeProcessing` automatically (RabbitMQ).** Messages accumulated into a batch aren't acknowledged until the whole batch is handled, so the RabbitMQ consumer's prefetch count is always sized to `MaxMessagesBeforeProcessing` — otherwise the broker would withhold messages beyond the prefetch limit until the in-flight (unacked) ones are acked, starving the accumulator down to one message per `MaxWaitTime` window regardless of publish rate. This isn't configurable — there is no `[MaxConcurrentCalls]` for stream handlers, since the batch loop only ever handles one batch at a time. InMemory has no broker prefetch concept, so this concern doesn't apply there — its in-process log simply retains everything not yet trimmed by retention. Azure Event Hubs reads through the SDK's own prefetch, which Flowly does not expose: the events it hands over are buffered per partition until the handler loop takes them.

### Start position

Every stream handler **must** explicitly set `StartPosition` in `Configure` — there is no default, and registration throws `InvalidOperationException` if it's left unset:

| Factory | Behavior |
|---|---|
| `StartPosition.First()` | Replay the entire retained stream from the beginning. The handler must be idempotent — every restart replays everything again. |
| `StartPosition.Last()` | Consume only messages published after the handler attaches. Messages already in the stream are never delivered, and nothing is delivered until a new message is published, on RabbitMQ (the `next` offset specification), InMemory and Azure Event Hubs (`EventPosition.Latest`) alike; on a partitioned stream this holds for every partition. Messages recorded while the process was down are missed after a restart, unless a [checkpoint](#position-persistence) holds a stored position, which takes precedence over `StartPosition`. |
| `StartPosition.Offset(n)` | Start at a specific numeric offset. On Azure Event Hubs the offset is the event's sequence number within its partition (sequence numbers start at zero in every partition). |
| `StartPosition.Timestamp(dt)` | Start at the first message at or after a point in time. On Azure Event Hubs this is the event's enqueued time; a `DateTime` of kind `Unspecified` is read as UTC. |

For `First()`/`Last()`, an attribute is also available as an alternative to setting it in `Configure`:

```csharp
[StreamStartPosition(StreamStartPositionKind.Last)]
internal class TelemetryReadingHandler : MessageStreamHandler<TelemetryReading>
{
    public override Task Handle(IMessageStreamContext<TelemetryReading> messageContext) => ...;
}
```

`Offset`/`Timestamp` have no attribute equivalent and still require `Configure`. If a handler overrides `Configure` and also sets `options.StartPosition` there, the `Configure` value wins over the attribute — same precedence `[BatchProcessing]` has.

> **No offset persistence by default.** Flowly does not checkpoint stream offsets across restarts unless you opt in — see [Position persistence](#position-persistence) below. `StartPosition` is re-evaluated fresh on every boot otherwise. Avoid values computed relative to the current time (e.g. `StartPosition.Timestamp(DateTime.UtcNow - TimeSpan.FromHours(2))`), which never converge across restarts without a checkpoint.

### Retention

Set retention limits on the message contract with `[StreamRetention]` so the stream doesn't grow unbounded:

```csharp
[StreamRetention(maxAgeSeconds: 604800, maxLengthBytes: 500_000_000)]
public record TelemetryReading(string SensorId, double Value);
```

Omitting both parameters means the stream retains every message forever — a broker disk exhaustion risk (InMemory: unbounded process memory growth instead — InMemory intentionally applies no extra default cap of its own, so this risk is identical across both transports). On Azure Event Hubs retention is configured on the event hub and these settings are not applied, see [Azure Event Hubs Transport](#azure-event-hubs-transport).

> **InMemory and `EnableReferencePassing`.** When the InMemory transport is configured with [`EnableReferencePassing = true`](#reference-passing), messages are never JSON-serialized, so `MaxLengthBytes` has no byte count to enforce and is silently ignored — only `MaxAgeSeconds` retention applies in that mode.

### Retry and failure handling

`[RetryPolicy(maxRetries, delaySeconds)]` on the handler class enables retry, but the mechanism is different from every other handler type: retries run **in-process**, re-invoking the handler on the same in-memory batch — messages are never re-published to the stream, which would permanently write retry noise into an immutable, replayable log.

Retries cover the whole processing of a batch, but only the failed step is repeated: once the handler has returned, a failure while acknowledging the messages or saving the [checkpoint](#position-persistence) is retried in-process with the same `[RetryPolicy]` count and delay without running the handler again, so a checkpoint store outage never causes a duplicate side effect inside a running process. When those retries are exhausted the partition halts like a handler failure, with a last error saying that the handler succeeded and the acknowledgement or checkpoint save failed, and a resume repeats only that step. Without `[RetryPolicy]` a single failing save halts the partition.

When retries are exhausted, the handler **halts consumption of that queue (or partition)** rather than skipping the failed batch: the stream offset never advances past it, a critical log entry and a `flowly.message.handler.halted` metric are emitted, and no further messages are processed. Flowly keeps the failed batch and applies backpressure instead of dropping anything: the transport stops receiving, the broker keeps the rest of the stream, and memory stays bounded (the held batch plus one further batch of buffered messages). The partition stays halted until it is revoked, which moves it to another instance that replays the failed batch from the stored position, or the process stops and the next start replays it; a halted batch can also be run again in place, which is an operator action and is never done automatically (a batch that fails again halts again), see [Operating halted partitions](#operating-halted-partitions-management-api-and-powershell). The partition is reported as `Halted` with its last error by the [partition state](#partition-state-and-health) metrics and health check. Dead letter tracking is not supported for stream handlers.

`MessageStreamHandlerOptions` does not inherit the queue options used by other handlers — `LockDuration`, `DeadLetterOnMessageExpiration`, and `Configure`-based retry settings don't apply to streams (there's no per-message peek-lock, and retention replaces TTL).

### Partition state and health

Every stream partition this process owns (a non-partitioned stream counts as one partition) is in one of three states, kept by Flowly itself and therefore the same on RabbitMQ, Azure Event Hubs and InMemory:

| State | Meaning |
|---|---|
| `Running` | A processing loop exists for the partition and is consuming. |
| `Waiting` | The partition's start position could not be resolved (for example the checkpoint store is unavailable), so no processing loop exists for it yet. Messages the transport still delivers for it are dropped and counted, and are redelivered from the stored position once the partition starts. On RabbitMQ the partition is retried without a restart, see [Recovery of a waiting partition on RabbitMQ](#recovery-of-a-waiting-partition-on-rabbitmq). On Azure Event Hubs the SDK retries it in every load balancing cycle, and with a `partitionOwnershipContainer` the entry is removed within about one cycle once another instance owns the partition. A non-partitioned stream reports it too while its start position cannot be resolved at startup: the service retries after 1 second, doubling up to 30 seconds, until it succeeds (the host keeps running); this retry interval is fixed. |
| `Halted` | The partition exhausted its in-process retries and stopped consuming; the stream offset does not advance past the failed batch. |

For each partition Flowly also keeps the last error (message and exception type) and when it happened, and when the partition entered its current state. A revoked partition is no longer reported. A halted partition is `Halted` for as long as it holds its batch and is `Running` again once the held batch is run again. Two metrics report them (see [OpenTelemetry](#metrics)): `flowly.message.stream.partition.state`, a gauge with the current state of each owned partition (`0` running, `1` waiting, `2` halted), and `flowly.message.stream.partition.transitions`, a counter of state changes tagged `state=running|waiting|halted`.

Register the health check to expose a stuck partition to your load balancer or orchestrator:

```csharp
builder.AddMessageStreamHealthCheck();
```

It is unhealthy while any owned partition is `Halted` or `Waiting` and healthy otherwise, including while the process owns no partition. The result lists each failing partition by queue, consumer and partition with its state, since when, and last error. A halted partition means consumption of it has stopped and needs operator action, so the default is `Unhealthy`; pass `failureStatus: HealthStatus.Degraded` to keep the instance in rotation. The check is registered under the name `flowly-message-streams` with the tag `message-streams` (both overridable) and works for every stream transport.

### Operating halted partitions: Management API and PowerShell

`Flowly.ManagementApi` lists the state of the stream partitions and resumes a halted one. The actions work on the live consumers of the instance that answers: each instance lists and resumes only the partitions it owns, there is no signalling between instances, and a partition is never handed to another instance on purpose. With several instances, call the one that owns the partition (a partition that moved to another instance is `404` here and `Halted` or `Running` there). They appear whenever Flowly is registered with `AddFlowly` (an instance without stream handlers lists nothing), under the same Read and Write authorization as the other actions.

| Action | Route | Authorization | Cmdlet |
|---|---|---|---|
| List the partitions of this instance | `GET {BasePath}/message-streams/partitions?queue=&consumer=&partition=&state=` | Read | `Get-FlowlyMessageStreamPartition [-QueueName] [-ConsumerName] [-Partition] [-State Running\|Waiting\|Halted]` |
| Resume a halted partition | `POST {BasePath}/message-streams/partitions/resume` with `{ "queueName", "consumerName", "partition" }` (`partition` is `null` for a non-partitioned stream) | Write | `Resume-FlowlyMessageStreamPartition -QueueName -ConsumerName [-Partition]` (supports `-WhatIf` and `-Confirm`, high impact) |

The list returns, per partition, the handler name, queue, consumer name, partition, state, when the state was entered and the last error (message, exception type, time). A resume re-runs the batch the partition is holding with a fresh set of in-process retries and lets the partition continue from it; it answers `204`. Nothing is skipped: when the batch fails again the partition halts again (and can be resumed again). It answers `404` when the instance does not own the partition and `409` when the partition is not `Halted` (a `Waiting` partition has no processing loop yet and is retried automatically, see [Recovery of a waiting partition on RabbitMQ](#recovery-of-a-waiting-partition-on-rabbitmq); there is no action that retries it sooner). Fix the cause first (the handler, a downstream dependency), then resume:

```powershell
Connect-Flowly -BaseUri https://myapp.example.com/flowly/management -ApiKey $env:FLOWLY_API_KEY
Get-FlowlyMessageStreamPartition -State Halted
Get-FlowlyMessageStreamPartition -State Halted | Resume-FlowlyMessageStreamPartition
```

### Recovery of a waiting partition on RabbitMQ

When the start position of a partition of a partitioned RabbitMQ stream cannot be resolved (for example the checkpoint store is down while the partition is assigned), Flowly recovers the partition without a process restart:

1. **The partition waits inside a bounded window.** The resolution is retried with a growing delay (0.5 s doubling up to 5 s) for up to 40 seconds, the time the broker allows a stream connection's start position callback before it closes the connection. Nothing is delivered for that partition while it retries, so an outage shorter than the window drops nothing, the partition is not reported as `Waiting` (and the health check stays healthy), and the other partitions keep consuming.
2. **If the window ends without a start position**, the partition is reported as `Waiting` from that moment and the broker's client attaches it at the end of the stream. Flowly has no processing loop for it, so what arrives for it is dropped, counted and never checkpointed; because nothing is checkpointed, the dropped messages are delivered again from the stored position once the partition starts. Nothing is skipped.
3. **The consumer is recreated automatically** while a partition is waiting: Flowly stops the partitions' processing loops, closes the Stream consumer and creates it again, so the broker offers every partition to this process once more and the start positions are resolved anew. The delay before a recreation starts at 5 seconds and doubles up to 2 minutes; only one recreation runs at a time, and the recovery ends when every partition has started or the host stops.
4. **After 3 consecutive recreations that still leave a partition waiting, the instance stands down.** It closes its consumer, so every partition it owns is revoked and the broker can hand them to another instance of the consumer group that may be able to resolve them (for example because only this instance has a broken connection to the checkpoint store), keeps the consumer closed for a cool-down and then creates it again. The cool-down is 1 minute and doubles with each following stand-down up to 5 minutes; it only delays, so an instance that is alone still recovers after every cool-down. The stand-down is logged as a warning and counted in the `flowly.message.stream.partition.stand_downs` metric (tagged with the queue and `consumer`, see [OpenTelemetry](#metrics)). The count and the cool-down reset when no partition is waiting any more. When the instance joins the group again the broker rebalances the partitions and may give the failing partition back to it, so the stand-down relieves a starving partition for the length of the cool-down and does not end the starvation while the instance still cannot resolve the start position.

The recreation re-initializes the healthy partitions too: each resumes from its checkpoint, and the batch it was processing when the consumer was closed was not checkpointed yet, so at most that batch is delivered again (handlers must be idempotent, as for every restart or rebalance). With other instances in the same consumer group the broker may hand partitions over between the instances during a recreation. The instance that owns the waiting partition retries it itself; the only deliberate hand-over is the stand-down, which also revokes the instance's healthy partitions (an instance that is alone consumes nothing during the cool-down) and releases the hold of a partition halted by its handler, which replays from the stored position wherever it is assigned next. A partition halted by its handler never starts a stand-down on its own.

While messages are dropped for a partition without a processing loop, Flowly logs one summary per partition at most every 30 seconds (number of dropped messages, last start position error) instead of one warning per message, and counts every drop in the `flowly.message.stream.partition.dropped` metric (tagged with the queue, `consumer` and `partition`, see [OpenTelemetry](#metrics)).

### Position persistence

Register a `MessageStreamCheckpoint<TMessage>` to restore restart-survival — a transport-agnostic extension point, backed either by one of Flowly's own database providers or by storage you implement yourself.

**Built-in (SQL Server, PostgreSQL, or SQLite)** — install `Flowly.MessageStreams.SqlServer`, `.Postgres`, or `.SQLite`, no hand-written storage code:

```csharp
builder.UseRabbitMq("RabbitMQ");
builder.AddSqlServerMessageStreamPersistence("MsgStreamDb");

builder.AddMessageStreamHandler<TelemetryReading, TelemetryReadingHandler>();
builder.UseMessageStreamCheckpoint<TelemetryReading>();
```

Multiple named persistence stores are supported in the same process — the first unnamed `Add<Provider>MessageStreamPersistence` call becomes the default, used when `UseMessageStreamCheckpoint<T>()` is called without a name:

```csharp
builder.AddPostgresMessageStreamPersistence("MsgStreamDb");
builder.AddSqlServerMessageStreamPersistence("MsgStreamDb", name: "This One");

builder.UseMessageStreamCheckpoint<TelemetryReading>("This One");
```

By default (`enableMigrations: true`), a hosted service applies pending EF Core migrations at startup. To generate an idempotent SQL script instead:

```bash
dotnet ef migrations script --idempotent --project Flowly.MessageStreams.SqlServer --output migrate.sql
```

**Custom storage** — implement `MessageStreamCheckpoint<TMessage>` yourself against whatever you already have (a cache, a file, a bespoke repository):

```csharp
internal class TelemetryReadingCheckpoint(IDbContextFactory<MyDbContext> dbContextFactory) : MessageStreamCheckpoint<TelemetryReading>
{
    protected internal override async Task InitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (await dbContext.StreamPositions.AnyAsync(p => p.ConsumerName == context.ConsumerName && p.Partition == context.Partition, cancellationToken))
            return;

        dbContext.StreamPositions.Add(new StreamPosition(context.ConsumerName, context.Partition));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    protected internal override async Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.StreamPositions
            .Where(p => p.ConsumerName == context.ConsumerName && p.Partition == context.Partition)
            .Select(p => p.Position)
            .SingleOrDefaultAsync(cancellationToken);
    }

    protected internal override async Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.StreamPositions
            .Where(p => p.ConsumerName == context.ConsumerName && p.Partition == context.Partition)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Position, context.Position), cancellationToken);
    }
}
```

```csharp
builder.AddMessageStreamCheckpoint<TelemetryReadingCheckpoint>();
// equivalent to: services.AddSingleton<MessageStreamCheckpoint<TelemetryReading>, TelemetryReadingCheckpoint>();
```

Flowly feature-detects the registration — no separate builder call is needed beyond the sugar above (or the plain `AddSingleton` it wraps). Once registered:

- `InitializeCheckpoint` runs before the processing loop starts (per reader, so per partition on a partitioned stream) and is called again the next time the reader starts if it threw, until it has completed once, so `SaveStreamPosition` — called after every successfully processed batch, and retried on its own without re-running the handler when it throws — can be a plain update with no existence check on the hot path.
- `GetStreamPosition` returning `null` means nothing has been saved for this reader yet — neither a processed batch nor a bootstrap seed. Flowly then resolves the `StartPosition` configured in `Configure`, which is only a bootstrap value, and (see below) stores it before consuming.
- **The first run's position is fixed up front.** For `StartPosition.Last()` and `StartPosition.Timestamp(...)`, when the transport can resolve the start to a concrete offset without consuming (RabbitMQ with a short AMQP probe consumer; Azure Event Hubs from the partition properties, which resolve `Last()` and a `Timestamp(...)` later than the last enqueued event; InMemory is not applicable because it rejects checkpoints), Flowly calls `SaveStreamPosition` once before the handler starts consuming, with `IsBootstrapSeed = true` and `Position` set to the first offset it will read minus one. A restart before the first batch therefore does not move the position, and messages published while the process was down are delivered — for example `Last()` on an empty stream, stopped before anything arrived. `First()` and `Offset(n)` do not change meaning between restarts and are not stored up front. A `Timestamp` is stored as a lower bound at the broker's chunk resolution, so a restart can redeliver messages from before the time but never skips one.
- **`SaveStreamPosition` can therefore be called without a processed batch, and `Position` can be `-1`** (the reader will start at offset `0`, as on an empty stream). Store negative values as they are, return a stored `-1` from `GetStreamPosition` as `-1` (not `null`), and do not treat every save as proof that a batch was handled; use `context.IsBootstrapSeed` to tell the two apart. The row may not exist yet when the seed is saved, so make `SaveStreamPosition` an upsert (the built-in stores already are). **A seed must never overwrite a stored position:** when `context.IsBootstrapSeed` is `true`, write only if no position is stored yet for the reader (a row created by `InitializeCheckpoint` without a position counts as none) and do nothing otherwise, as one atomic operation (a conditional insert or an update that requires an empty position) rather than a read followed by a write. A seed can complete late, after the reader has started and saved a higher position (for example when resolving the start position was abandoned), and an unconditional write would move the position backwards so that the next restart replays everything in between. Flowly also re-reads the position with `GetStreamPosition` just before it saves a seed and skips the seed when one now exists (the reader then resumes after that position), which narrows the window for a store that does not follow the rule yet but does not close it. A save without the flag always overwrites; the built-in stores follow the rule.
- If the transport cannot resolve the position (the broker did not answer in time, or the stream reports messages but delivered none), or the store or resolver fails, Flowly logs a warning, counts it (`outcome=seed_failed`) and starts with the configured `StartPosition`; the position is then stored after the first processed batch as before.
- The position saved is never for a batch still retrying or that failed — a crash mid-batch replays at most the last unsaved batch on restart, consistent with the in-process retry model above.

**Checkpoint identity.** Two different key fields on `MessageStreamCheckpointContext` disambiguate readers: `ConsumerName` (defaults to the handler type name; override with `options.ConsumerName` in `Configure` if the handler class might be renamed later) separates independent readers of the same stream — e.g. two different services each replaying it independently — and `Partition` (`null` for a non-partitioned stream; the owning partition index for a [partitioned](#partitioned-streams) one).

**Not for InMemory.** Registering a checkpoint against an InMemory-backed stream throws `InvalidOperationException` at registration time — the underlying log has no cross-restart persistence of its own, so a persisted position would point at data that no longer exists after a restart. RabbitMQ and Azure Event Hubs streams support checkpoints; on Azure Event Hubs the stored position is the sequence number of the last processed event of the partition, and a stored position wins over `StartPosition` exactly as on RabbitMQ.

**Run at most one live instance** of a given non-partitioned handler registration against a shared checkpoint store at a time. Flowly does not coordinate exclusive access across processes — running more than one concurrently will corrupt the stored position. (Partitioned streams are unaffected by this constraint — see below.)

**Implementations must be thread-safe.** The checkpoint is registered `AddSingleton`, so one instance is shared for the process's lifetime. For a non-partitioned stream that's never a concern — one reader, one sequential loop. For a [partitioned](#partitioned-streams) stream, Flowly runs one independent processing loop per partition currently owned by this process, and every loop calls into the *same* checkpoint instance — `InitializeCheckpoint`, `GetStreamPosition`, and `SaveStreamPosition` can all run concurrently across different partitions (never twice concurrently for the same `(ConsumerName, Partition)`, but across different ones, yes). This is why the example above injects `IDbContextFactory<MyDbContext>` and creates a short-lived `DbContext` per call rather than taking a `DbContext` directly through the constructor — an EF Core `DbContext` is not safe for concurrent use, and a constructor-injected one (normally registered `Scoped`) captured as state on a `Singleton` is a DI captive-dependency bug on top of that.

**Resetting a position from outside the running process** — useful after an integration issue, to replay messages from an earlier offset. Two tools, both reading/writing the same underlying store:

```bash
# Flowly.Tool — works against any deployment with network access to the checkpoint database
flowly message-stream list-checkpoints --provider sqlite --connection "Data Source=checkpoints.db"
flowly message-stream reset-checkpoint --provider sqlite --connection "Data Source=checkpoints.db" \
  --queue my-queue --consumer MyHandler --offset 42 --yes
```

```powershell
# Flowly.PowerShell — talks to a running Flowly.ManagementApi instance over HTTPS instead
Connect-Flowly -BaseUri https://myapp.example.com/flowly/management -ApiKey $env:FLOWLY_API_KEY
Get-FlowlyMessageStreamCheckpoint
Reset-FlowlyMessageStreamCheckpoint -QueueName my-queue -ConsumerName MyHandler -Offset 42
```

Either way, the reset only takes effect the next time that handler's process starts — `StartPosition` is resolved once at boot, not polled while running. See `Flowly.ManagementApi`'s own README for authentication options (API key or OAuth2/Entra ID client-credentials) if you're deployed somewhere the checkpoint database isn't directly reachable.

### Partitioned streams

Divide a stream into `N` independent, ordered sub-logs with `[StreamPartitions(count)]` on the message contract:

```csharp
[StreamPartitions(4)]
public record TelemetryReading(string SensorId, double Value);
```

Recording onto a partitioned stream takes an optional partition key — messages recorded with the same key always land in the same partition (an ordering guarantee they wouldn't otherwise have); omitting it distributes round-robin:

```csharp
await messageRecorder.Record(reading, ct, partitionKey: reading.SensorId);
```

The handler is unchanged — `MessageStreamHandler<T>` and `Configure` work exactly as for a non-partitioned stream. `IMessageStreamContext<T>.Partition` reports which partition the current batch came from (`null` for non-partitioned streams):

```csharp
public override async Task Handle(IMessageStreamContext<TelemetryReading> ctx)
{
    logger.LogInformation("Processing partition {Partition}", ctx.Partition);
    foreach (var reading in ctx.Messages)
        await Save(reading);
}
```

**Supported on RabbitMQ, InMemory and Azure Event Hubs** — all three implement `IPartitionedStreamCapableMessageBusClient`; registering against Azure Service Bus throws `InvalidOperationException` at registration time, same as non-partitioned streams.

**Flowly does not implement its own cross-instance partition-assignment protocol.** Each transport owns partition ownership and rebalancing using its own native mechanism:

- **RabbitMQ** uses [Super Streams](https://www.rabbitmq.com/docs/streams#super-streams) with broker-coordinated **Single Active Consumer** — only one of your running instances is ever actively reading a given partition at a time, and RabbitMQ hands ownership off automatically as instances join or leave. This needs the `RabbitMQ.Stream.Client` package (pulled in automatically by `Flowly.RabbitMQ`) alongside the classic AMQP client Flowly already uses for everything else — only partitioned *consumption* needs it; topology creation and producing both stay on plain AMQP.
- **Azure Event Hubs** maps partition `k` to the event hub partition with the id `k` and leaves ownership to the Azure Event Hubs SDK: the instances that share a consumer group (the handler's `ConsumerName`) and a `partitionOwnershipContainer` are balanced across the partitions by the SDK's own load balancing, which Flowly translates into partition assigned and revoked events without adding a protocol of its own. A halted partition keeps the SDK's ownership while it is halted (its batch callback stays blocked), but is still moved by rebalancing when an instance joins. See [Azure Event Hubs Transport](#azure-event-hubs-transport).
- **InMemory** assigns every partition to the one process immediately and keeps it forever — there's no cross-instance ownership to hand off in a single process. This makes InMemory partitioned streams useful for developing and testing partition-aware handler code without a broker, but it does **not** give you the throughput/scale-out benefit partitioning exists for on RabbitMQ — that fundamentally needs more than one process.

A halted partition (retries exhausted, per [Retry and failure handling](#retry-and-failure-handling)) only holds that partition — it stops receiving while the broker keeps its data, and other partitions, and the handler process itself, keep running.

> **RabbitMQ partitioned consumption uses a separate connection port from AMQP** (the Stream protocol, port `5552` by default, distinct from AMQP's `5672`) on the same broker host. Configure a different port via `UseRabbitMq(..., streamPort: <port>)` — the stream protocol is always assumed to be on the same host as the AMQP connection, only the port is configurable. If you change your deployment's published stream port, pass the matching `streamPort` value.

The broker must also advertise a stream host and port the client can reach (`stream.advertised_host` and `stream.advertised_port` in `rabbitmq.conf`): after the first connection, the connection that consumes goes to the address the broker advertises, not to the configured `streamPort`. This only matters when the AMQP host is not loopback — the client keeps the configured endpoint for a loopback host, which is why the samples and templates only set `stream.advertised_host = localhost`. A load balancer in front of the stream port is not supported.

**Rebalancing and crashes are at-least-once.** When an instance joins, leaves or crashes, the new owner of a partition resumes from the stored checkpoint (`StartPosition` is only a bootstrap value, stored for that partition before its first batch). The batch that was in flight on the previous owner has not been checkpointed, so it is handled again — at most one batch per affected partition. Handlers on partitioned streams must be idempotent.

**On RabbitMQ, a partition count on the broker that differs from `[StreamPartitions(n)]` fails host startup.** For each queue carrying `[StreamPartitions(n)]`, Flowly queries the broker for the Super Stream's actual partition streams and throws `InvalidOperationException` naming the stream, the declared count and the broker's count if they differ (or, with `createTopology: false`, if the Super Stream doesn't exist at all). This catches drift between your code and the broker before it causes silent data loss — an undetected mismatch would let the sender compute publish routing keys with no matching exchange binding (dropped without error) or leave real broker partitions permanently unconsumed. The check runs with both `createTopology: false` and `createTopology: true`. With `createTopology: true` (the default), Flowly first declares any *missing* partition streams and bindings, so a broker with fewer partitions than declared is topped up and starts, while a broker with *more* partitions than declared (for example after lowering `n`) fails. Flowly never deletes or re-creates partition streams or bindings to make the broker match the contract: resolving drift is an operator action on the broker. **Changing the partition count of a live stream is not supported** — re-partitioning changes which partition a key maps to, so it is not offered; a changed `n` is treated as drift and fails startup. Because the check uses the Stream protocol, a host that only sends to a partitioned stream also needs the stream port (`streamPort`) to be reachable at startup when the check runs. **A Super Stream created outside Flowly (for example with `rabbitmq-streams add_super_stream`) is used with `createTopology: false`.** Flowly's own topology declaration is not made compatible with Super Streams created that way: with `createTopology: true` the broker rejects the declaration (`PRECONDITION_FAILED`, an inequivalent `x-queue-leader-locator` argument) before the partition count is compared. With `createTopology: false` such a Super Stream is consumed and published to unchanged, and the partition count check still runs.

---

## Events (Fan-Out)

Events let multiple independent services each receive a copy of the same occurrence. Use events when several consumers need to react to the same thing — as opposed to a regular message, where only one handler processes each message.

### Publishing an event

Register a submitter and inject `IEventSender`:

```csharp
builder.AddEventSubmitter<OrderProcessed>();
```

```csharp
public class OrderService(IEventSender eventSender)
{
    public async Task CompleteOrder(Order order, CancellationToken ct)
    {
        await eventSender.RaiseEvent(new OrderProcessed(order.Id), ct);
    }
}
```

### Subscribing to an event

Inherit `EventHandlerBase<TEvent>` and register the handler:

```csharp
public class OrderProcessedEventHandler : EventHandlerBase<OrderProcessed>
{
    public override Task Handle(IEventContext<OrderProcessed> ctx, CancellationToken ct)
    {
        // runs in every service that registers this handler
        return Task.CompletedTask;
    }
}
```

```csharp
builder.AddEventHandler<OrderProcessed, OrderProcessedEventHandler>();
```

### Event and subscription naming

Names are resolved by `KebabCaseTopologyNameResolver` (the built-in default). See [Topology Name Resolution](#topology-name-resolution) to replace the strategy entirely.

| What | Rule |
|---|---|
| Topic / exchange name | Derived from event type: PascalCase → kebab-case, strip trailing `Event` (`OrderProcessedEvent` → `order-processed`) |
| Subscription / queue name | Derived from handler class name: PascalCase → kebab-case (`OrderProcessedEventHandler` → `order-processed-event-handler`) |
| Override topic name | `[EventName("custom-name")]` on the event type |

### Subscription name uniqueness across services

The subscription name is derived from the **handler class name**. Two services that both define a class called `OrderProcessedEventHandler` will derive the same subscription name (`order-processed-event-handler`) and end up sharing a single subscription — meaning only one of them receives each event instead of both.

**Each subscriber service must use a distinct handler class name.** Prefix the class name with the service or domain context:

```csharp
// BackendProcessor — subscription: "order-processed-event-handler"
public class OrderProcessedEventHandler : EventHandlerBase<OrderProcessed> { ... }

// BackendFinanceProcessor — subscription: "finance-order-processed-event-handler"
public class FinanceOrderProcessedEventHandler : EventHandlerBase<OrderProcessed> { ... }
```

Flowly cannot detect name collisions across separately deployed services, so uniqueness must be maintained by convention.

### Dead letter tracking for events

Event handlers support `.WithDeadLetterTracking()` the same way regular handlers do:

```csharp
builder
    .AddSqlServerDeadLetterTracking("DeadLetters")  // or AddPostgresDeadLetterTracking / AddSQLiteDeadLetterTracking; accepts a connection name or literal connection string
    .AddEventHandler<OrderProcessed, OrderProcessedEventHandler>()
    .WithDeadLetterTracking();
```

When a dead-lettered event is requeued, Flowly re-publishes it to the topic with a `flowly-target-subscription` header. Only the originating subscriber's filter accepts the message, so only that subscriber receives the requeued event.

---

## Handler steps and send steps

Cross-cutting behavior such as a logging scope, an audit trail, validation, ambient context, a unit of work, idempotency or authorization ends up repeated in every handler and every call site. Flowly has two seams for it, both registered once per `Configuration` and selected by message type:

- A **handler step** wraps the `Handle` call of a handler on the receiving side.
- A **send step** runs in the caller's process before a message is handed to the transport on the sending side.

Use a step when the same concern applies to many messages. Keep logic that belongs to one message in its handler.

### Writing a handler step

Derive from `HandlerStep<TFilter>`. The step runs only for messages whose runtime type is assignable to `TFilter`; the non-generic `HandlerStep` runs for every message. `Handle(context, next)` is the primitive: the default implementation awaits `OnBeforeHandlerExecution`, then `next()` (the inner steps and the handler), then `OnAfterHandlerExecution`. Override the hooks for simple cases and `Handle` when you need to wrap the handler.

An audit step needs only the after hook:

```csharp
public interface IEnableAuditLog;

public record OrderPlaced(Guid OrderId, decimal Total) : IEnableAuditLog;

public class AuditStep(IAuditLog auditLog) : HandlerStep<IEnableAuditLog>
{
    protected override Task OnAfterHandlerExecution(HandlerStepContext<IEnableAuditLog> context)
        => auditLog.Append(context.Message, context.Properties.MessageId, context.CancellationToken);
}
```

A logging scope has to stay open around the handler, so it overrides `Handle`:

```csharp
public class LoggingScopeStep(ILogger<LoggingScopeStep> logger) : HandlerStep
{
    public override async Task Handle(HandlerStepContext<object> context, HandlerStepDelegate next)
    {
        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["MessageId"] = context.Properties.MessageId,
                   ["CorrelationId"] = context.Properties.CorrelationId
               }))
        {
            await next();
        }
    }
}
```

An idempotency step skips the handler by not calling `next`:

```csharp
public class IdempotencyStep(IProcessedMessageStore store) : HandlerStep
{
    public override async Task Handle(HandlerStepContext<object> context, HandlerStepDelegate next)
    {
        if (await store.Contains(context.Properties.MessageId, context.CancellationToken))
        {
            return;
        }

        await next();
        await store.Add(context.Properties.MessageId, context.CancellationToken);
    }
}
```

`IAuditLog` and `IProcessedMessageStore` are your own services. The context carries the typed `Message`, the transport-level `Properties` (`MessageId`, `CorrelationId`, `RetryCount`, `DeliveryCount`, ...), the `HandlerType` (the concrete handler class, so a step can inspect its attributes) and the `CancellationToken` of the delivery. The `HandlerStepContext<T>` constructor is public, so a step can be unit tested with a hand-built context.

### Selecting messages with a marker interface

The filter type is usually an interface the message contract implements, in the same way a contract owns `[QueueName]` or `IOpenTelemetryTagsProvider`. `AuditStep` above runs for `OrderPlaced` because it implements `IEnableAuditLog` and for no other message. A base class works as a filter too, and `HandlerStep` (equivalent to `HandlerStep<object>`) needs no marker. Matching uses the runtime type of the message and is cached per type.

### Registering handler steps

```csharp
public class ReceiverConfig : Configuration
{
    public override void Configure(IFlowlyBuilder builder) =>
        builder
            .AddMessageHandler<OrderPlaced, OrderPlacedHandler>()
            .AddHandlerStep<LoggingScopeStep>()   // outermost
            .AddHandlerStep<IdempotencyStep>()
            .AddHandlerStep<AuditStep>();         // innermost, closest to the handler
}
```

- A step applies to every message, event, call and job handler registered in the same `Configuration` whose message type matches the filter. It does not matter whether the step is registered before or after the handler. There is no per-handler attachment or opt-out.
- The first registered step is the outermost. Order matters: with the order above a duplicate message is skipped before the audit step runs.
- Steps are scoped services resolved from the same per-message scope as the handler, so a step and the handler share scoped services such as one `DbContext`.
- A step that matches the message type of no registered handler, and a step type registered twice, are reported as warnings at host start-up.

### What a step wraps

A step wraps exactly the user's `Handle` call. Corrupt-body dead lettering, retry republishing, completion, dead lettering, job state messages and call replies stay outside the steps, and the steps run inside the Flowly handling span, so `Activity.Current` is the Flowly span. Flowly emits no span or metric per step; a step can start its own `Activity`.

- **Exceptions.** An exception from a step or the handler unwinds through the enclosing steps (their `finally` blocks run) and then takes Flowly's existing retry and dead letter path unchanged.
- **Retries.** Steps run again on every attempt, because a retry is a new delivery. `Properties.RetryCount` tells a step which attempt it is.
- **`OnAfterHandlerExecution`** runs only when the inner steps and the handler completed without an exception. A step that needs to react to a failure overrides `Handle` and uses `try`/`catch`/`finally`.
- **Not calling `next` skips the handler and every inner step, and the delivery completes as a success.** This is how an idempotency step works, and it is the only short-circuit mechanism.
- **A step that catches an exception and does not rethrow also makes the delivery succeed.** A message that "succeeds" without the handler having run is hard to diagnose, so use both capabilities deliberately.
- **Cancellation.** `CancellationToken` is the token the handler receives, cancelled on shutdown. To stop, do not call `next` or throw.

### Rejecting messages permanently

Throw `MessageRejectedException(reason)` from a step to reject a message that retrying cannot fix, for example when it is invalid or the sender is not authorized:

```csharp
public class PositiveTotalStep : HandlerStep<OrderPlaced>
{
    protected override Task OnBeforeHandlerExecution(HandlerStepContext<OrderPlaced> context)
        => context.Message.Total > 0
            ? Task.CompletedTask
            : throw new MessageRejectedException("Order total must be positive");
}
```

Flowly dead-letters the message immediately with the reason, regardless of `[RetryPolicy]`; a job handler fails the job with the reason instead. A domain exception that must not depend on Flowly is registered with `options.NonRetryable<TException>()` and gets the same treatment, subclasses included:

```csharp
builder.AddFlowly<ReceiverConfig>(options => options.NonRetryable<NotAuthorizedException>());
```

See [Rejecting a message without retries](#rejecting-a-message-without-retries) for the handler kinds this covers.

### Handler kinds covered

| Handler | Steps | Notes |
|---|---|---|
| `MessageHandler<T>` | Yes | Reference case |
| `EventHandlerBase<TEvent>` | Yes | Each subscriber has its own delivery, scope, retry and dead letter path; steps filter on the runtime type of the event |
| `JobHandler<T>` | Yes | Steps run between the `Started` and `Completed` job state messages while the alive signal keeps running. A step that throws fails the job attempt like the handler; a step that does not call `next` completes the job without running the handler |
| `CallHandler<T, TReturn>` | Request side | A step sees the request, never the response (see the limitations below) |
| `BatchMessageHandler<T>` | No | |
| `MessageStreamHandler<T>` | No | |
| `RecurringJobHandler` | No | The handler receives no message, so a message filter has nothing to match |

### Writing a send step

Derive from `SendStep<TFilter>` (or the non-generic `SendStep` for every message) and override `Handle(context, next)`. `next()` performs the actual send. The context carries the `Message` and the `CancellationToken`. This example authorizes the current user before a command is published:

```csharp
public interface IRequiresPermission
{
    string Permission { get; }
}

public record ApproveInvoice(Guid InvoiceId) : IRequiresPermission
{
    public string Permission => "invoices.approve";
}

public class NotAuthorizedException(string permission) : Exception($"Missing permission '{permission}'");

public class RequirePermissionSendStep(ICurrentUser user, IAuthorization authorization) : SendStep<IRequiresPermission>
{
    public override async Task Handle(SendStepContext<IRequiresPermission> context, SendStepDelegate next)
    {
        if (user.Principal is not { Identity.IsAuthenticated: true })
        {
            throw new NotAuthorizedException(context.Message.Permission);
        }

        if (!await authorization.IsAllowed(user.Principal, context.Message.Permission, context.CancellationToken))
        {
            throw new NotAuthorizedException(context.Message.Permission);
        }

        await next();
    }
}
```

Register it on the sending side's `Configuration`:

```csharp
public class SenderConfig : Configuration
{
    public override void Configure(IFlowlyBuilder builder) =>
        builder
            .AddMessageSubmitter<ApproveInvoice>()
            .AddSendStep<RequirePermissionSendStep>();
}
```

`ICurrentUser` and `IAuthorization` are your own services. Send steps are a separate type from handler steps because sender and receiver are separate deployables: a step that exists on the sending side does not pull its dependencies into the receiving process. Filtering and ordering follow the same rules as handler steps (the first registered step is the outermost).

Send steps run for `IMessageSender.Send`, `IMessageRecorder.Record`, `IEventSender.RaiseEvent`, `IMessageCaller.Call` and `IJobMessageSender.QueueJob`:

- An exception thrown by a step reaches the caller of the send unchanged. Nothing is published and there is no retry. Throwing a domain exception is the normal way to reject.
- A step that does not call `next` silently drops a `Send`, `Record` or `RaiseEvent`.
- A `Call` needs a reply and a `QueueJob` needs a `JobId` to return, so when a step does not call `next` they throw an `InvalidOperationException` stating that a send step prevented the call or job.
- For `Call`, the steps run before the pending call is registered and before the call timeout starts, so a step that throws rejects the call immediately instead of ending in a timeout.
- For `QueueJob`, the steps run before anything is sent, so a rejected job leaves no orphan job record.
- Steps run outside the send span and metrics, so a rejected send is not counted as a failed send.
- Flowly's own internal messages, such as job state and scheduling messages, and retry, reply, dead letter requeue and recurring-job scheduler sends never pass through send steps.

### Send step scope and identity

`IMessageSender`, `IMessageRecorder`, `IEventSender`, `IMessageCaller` and `IJobMessageSender` are singletons, so a send step cannot be resolved from the caller's request scope. Flowly creates a fresh dependency injection scope for each send, only when at least one step matches the message, and resolves the steps from it. With no matching step the send is a direct call.

The consequence is that a scoped service the caller's own middleware populated is a different, unpopulated instance inside the step. Take identity from a source that is not bound to the DI scope. A good pattern is an `ICurrentUser` that is a thin reader of `IHttpContextAccessor` and is never populated per request, so it returns the same answer in a controller, in a handler of the request and in a send step:

```csharp
public interface ICurrentUser
{
    ClaimsPrincipal? Principal { get; }
}

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
}

services.AddHttpContextAccessor();
services.AddSingleton<ICurrentUser, CurrentUser>();
```

`IHttpContextAccessor.HttpContext` is visible in the step when the send is awaited within the request's async flow. Outside a request (a hosted service, or a handler sending follow-up messages) there is no principal, so a step must handle `Principal` being `null` explicitly, as `RequirePermissionSendStep` does.

### Trace context

`Activity.Current` is async-local, so it flows through the per-send scope unchanged: inside a step it is the caller's activity (for an ASP.NET Core request, the request activity), the `flowly.send` span is its child, and the trace continues into the receiver. A step that starts its own `Activity` propagates correctly as long as that activity is current while `next()` runs.

### Limitations

- **Handler kinds.** Steps cover message, event, job and call handlers (the call's request side). Batch, stream and recurring handlers are not covered.
- **No replacing.** `next` takes no arguments: a step cannot replace the message, the cancellation token or, for a call, the response. A step never sees the response of a call handler.
- **No per-message timeout step.** A timeout step would have to hand the handler a different cancellation token, which a step cannot do.
- **Call handlers.** A call handler that is rejected or skipped by a step, or that fails, produces no reply, so the caller times out. "Audited" means the handler finished, not that the reply was delivered: if sending the reply fails, the delivery is retried and the handler and its steps run again, so an audit store should key on `MessageId` plus `RetryCount`.
- **No transaction sharing on the sending side.** A send step cannot join the caller's `DbContext` or transaction, so send steps cannot implement a transactional outbox.
- **Identity on the receiving side.** `MessageProperties` has no identity field. The user can travel in the message (for example an `IssuedBy` property on the command) or in the unauthenticated custom message properties (`MessageProperties.CustomProperties`, see "Custom message properties"), which a send step cannot stamp yet because it has no access to the outgoing properties. A receiver step on the same marker is a useful second guard that catches a sender lacking its send step, but it cannot defend against someone publishing manually: whoever can publish can forge the identity. The broker's access control is the real boundary.

---

## Topology Name Resolution

Flowly resolves queue names, event topic names, and subscription names through an `ITopologyNameResolver`. The built-in default is `KebabCaseTopologyNameResolver`, which applies the kebab-case rules described in [Queue name auto-generation](#queue-name-auto-generation) and [Event and subscription naming](#event-and-subscription-naming).

### Built-in resolvers

| Resolver | Separator | Example | Idiomatic for |
|---|---|---|---|
| `KebabCaseTopologyNameResolver` | `-` | `process-order` | Default; Azure Service Bus |
| `DotCaseTopologyNameResolver` | `.` | `process.order` | RabbitMQ |

Both are in the `Flowly.MessageInfrastructure` namespace. The RabbitMQ project templates (`flowlyapp --transport rabbitmq`, `flowlyaspireapp --transport rabbitmq`, `flowly --transport rabbitmq`) automatically register `DotCaseTopologyNameResolver`.

### Custom resolver

Implement `ITopologyNameResolver` and register it via `FlowlyOptions`:

```csharp
public interface ITopologyNameResolver
{
    string ResolveQueueName<TMessage>();
    string ResolveEventName<TEvent>();
    string ResolveSubscriptionName<THandler>();
}
```

Register your resolver in `AddFlowly`:

```csharp
builder.AddFlowly(
    options => options.WithTopologyNameResolver<MyTopologyNameResolver>(),
    flowlyBuilder => flowlyBuilder
        .UseAzureServiceBus("AzureServiceBus")
        .AddMessageHandler<OrderCreated, OrderCreatedHandler>());
```

**Constraint: no dependency injection.** Topology name resolution happens at registration time — before the application's DI container is built — so the resolver cannot receive constructor-injected dependencies. Your implementation must have a public parameterless constructor and be self-contained.

### Example: SCREAMING_SNAKE_CASE resolver

```csharp
using System.Text.RegularExpressions;

public class UpperSnakeCaseTopologyNameResolver : ITopologyNameResolver
{
    public string ResolveQueueName<TMessage>()
    {
        var attribute = typeof(TMessage).GetCustomAttribute<QueueNameAttribute>();
        return ToUpperSnake(attribute?.QueueName ?? DeriveFromTypeName<TMessage>("Message"));
    }

    public string ResolveEventName<TEvent>()
    {
        var attribute = typeof(TEvent).GetCustomAttribute<EventNameAttribute>();
        return ToUpperSnake(attribute?.Name ?? DeriveFromTypeName<TEvent>("Event"));
    }

    public string ResolveSubscriptionName<THandler>()
        => ToUpperSnake(typeof(THandler).Name);

    private static string DeriveFromTypeName<T>(string suffix)
    {
        var name = typeof(T).Name;
        if (name.EndsWith(suffix, StringComparison.Ordinal))
            name = name[..^suffix.Length];
        return name;
    }

    private static string ToUpperSnake(string name)
        => Regex.Replace(name, @"(?<=[a-z])(?=[A-Z])", "_").ToUpperInvariant();
}
```

With this resolver, `OrderCreatedMessage` resolves to `ORDER_CREATED` instead of `order-created`. `[QueueName]` and `[EventName]` attribute values are also passed through `ToUpperSnake`, so they are normalised consistently.

---

## Retry Policy

When a handler throws, Flowly can retry the message automatically before giving up.

```csharp
[RetryPolicy(maxRetries: 3, delaySeconds: 30)]
public class OrderCreatedHandler : MessageHandler<OrderCreated>
{
    public override async Task Handle(IMessageContext<OrderCreated> ctx)
    {
        // If this throws, Flowly retries up to 3 times with 30-second gaps.
    }
}
```

**How it works:**

1. Handler throws an exception
2. If retries remain: Flowly re-publishes the message to the same queue, scheduled `delaySeconds` in the future, with an incremented retry counter in the message metadata
3. The original message is acknowledged and removed from the queue
4. On the next delivery the handler runs again with the updated retry count
5. When all retries are exhausted: the message is dead-lettered at the broker level

For job handlers, exhausted retries transition the job to `Failed` in the database — the message is completed rather than dead-lettered.

Retry policy applies to `MessageHandler<T>`, `JobHandler<T>`, and `BatchMessageHandler<T>`. Recurring jobs do not support retry. See the [Batch handler](#batch-handler) section for delivery-semantics details specific to batch handlers.

### Rejecting a message without retries

Some failures are permanent: retrying an invalid or unauthorized message only delays the inevitable. Two mechanisms mark a failure as non-retryable. Both skip `[RetryPolicy]` and dead-letter the message immediately, using the exception message as the dead letter reason (a job handler fails the job with that reason instead). They apply to message handlers, event handlers, call handlers and job handlers, including the handler steps that run around them. They have no effect on batch handlers and message stream handlers.

Throw `MessageRejectedException` from a handler or a step when you want to reject explicitly:

```csharp
throw new MessageRejectedException("Order total must be positive");
```

Register an exception type with `NonRetryable<TException>()` to keep a domain exception free of any dependency on Flowly. The type and every subclass of it become non-retryable:

```csharp
builder.AddFlowly<MyConfig>(options => options.NonRetryable<NotAuthorizedException>());
```

For a call handler the message is dead-lettered and the caller times out, exactly as for any other exhausted failure.

---

## Dead Letter Tracking

When messages are dead-lettered (after retries are exhausted, or because they couldn't be deserialized), Flowly can capture them in a database so you can inspect and act on them later.

### Setup

Register the persistence layer once, then opt individual handlers in. There are two patterns:

**Co-located (same project as the handler):**
```csharp
builder
    .AddSqlServerDeadLetterTracking("DeadLetters")  // or AddPostgresDeadLetterTracking / AddSQLiteDeadLetterTracking; accepts a connection name or literal connection string
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>()
    .WithDeadLetterTracking();                          // this handler's DLQ is tracked
```

**Standalone tracker project** (separate deployable, no consumer on the main queue):
```csharp
// In a dedicated DeadLetterTracker project — connects to transport but registers no handler
builder.AddSqlServerDeadLetterTracking("DeadLetters")
builder.AddDeadLetterSource<OrderCreated>();            // monitors the dead-letter sub-queue only
```

Calling either method without a persistence layer registered throws at startup.

**Read-only client** (e.g. a standalone Dashboard project that only needs to browse, requeue or discard dead letters):
```csharp
builder.AddDeadLetterTrackingClient("DeadLetters");     // or the Postgres / SQLite equivalent; accepts a connection name or literal connection string
```

`AddDeadLetterTrackingClient` registers only the data context, repository and `IDeadLetterService` — no ingestion, cleanup or metrics background services and no migrations — so it never participates in ingesting dead letters. It is provided by each backend package (`Flowly.DeadLetters.SqlServer`, `.Postgres`, `.SQLite`).

**Querying:** `IDeadLetterService.QueryDeadLetters(new DeadLetterQuery { QueueName = "orders", Status = DeadLetterStatus.Pending, Skip = 0, Take = 50, IncludeTotalCount = true })` returns a `DeadLetterPage` (`Items`, newest first, and `TotalCount` when requested). The database filters (`QueueName`, `SubscriptionName`, `Status`, `MessageId`), orders and pages, so only the requested page is loaded; `GetDeadLetters()` loads every tracked record.

### What gets stored

| Field | Description |
|---|---|
| `Id` | Unique identifier |
| `QueueName` | The queue the message came from |
| `MessageBody` | Raw message body (never deserialized) |
| `MessageProperties` | All metadata headers as JSON |
| `DeadLetteredAt` | When the message was dead-lettered |
| `DeadLetterReason` | Broker-provided reason |
| `DeadLetterErrorDescription` | Broker-provided error detail |
| `Status` | `Pending`, `Requeued`, or `Discarded` |
| `RequeuedAt` / `RequeuedBy` | Set when a message is requeued |
| `ReplacementMessageBody` | The corrected body the message was requeued with; `null` when the stored body was requeued as it was (see [Fix and requeue](#fix-and-requeue-a-dead-letter)) |

The raw body is stored without deserialization — this ensures malformed messages (which may be the reason for dead-lettering) are preserved exactly as received.

### Supported handler types

Dead letter tracking is supported on `MessageHandler<T>` and `EventHandlerBase<TEvent>` handlers. Job handlers use the job database as the failure record. Recurring jobs re-trigger via the CRON scheduler.

For event handlers, the `SubscriptionName` field in the `DeadLetters` table identifies which subscriber dead-lettered the event. Requeuing re-publishes to the topic with a `flowly-target-subscription` header so only the originating subscriber receives the requeued event.

### Fix and requeue a dead letter

A message that was dead-lettered because of a faulty payload can be corrected before it is requeued. `IDeadLetterService.Requeue(messageId, requeuedBy, replacementMessageBody)` republishes `replacementMessageBody` instead of the stored body, with the stored content type and application properties (event subscription dead letters are still addressed to the originating subscriber only). The republished message gets a new message id, so if it is dead-lettered again it is tracked as a separate record while the original keeps its id and its `Requeued` status. The same optional replacement is available as `messageBody` on the Management API requeue action, as `-MessageBody` on `Submit-FlowlyDeadLetter` and in the Dashboard, whose requeue dialog shows the stored body in an editable text area and only sends it when it was changed.

```csharp
await deadLetterService.Requeue(messageId, requeuedBy: "ops@example.com", replacementMessageBody: """{"orderId":42,"amount":12.5}""");
```

The stored `MessageBody` is never changed. After the republish has succeeded, the replacement is recorded in the nullable `ReplacementMessageBody` column of the `DeadLetters` table (`null` means the stored body was sent as it was), together with the requeue status, so the original and the correction are both kept for audit and a failed publish leaves the record `Pending` with no replacement. The column is added by an EF Core migration of each backend package, applied automatically unless `enableMigrations` is `false`; with `enableMigrations: false`, apply the `AddReplacementMessageBody` migration (a nullable text column) before the new version runs. A replacement must not be empty or whitespace, must not exceed 256 KiB as UTF-8 (the smallest broker message limit, Azure Service Bus Standard) and, when the stored body is valid JSON, must be valid JSON too; otherwise `InvalidReplacementMessageBodyException` is thrown (an `ArgumentException`) and nothing is published. `IDeadLetter.ReplacementMessageBody` exposes the recorded replacement; custom `IDeadLetterService` implementations keep compiling, because the replacement overload is a default interface member that requeues as before without a replacement and throws `NotSupportedException` with one.

### Operating dead letters: Management API and PowerShell

`Flowly.ManagementApi` lists, requeues and discards the dead letters of a host that registered dead letter tracking (`Add<Provider>DeadLetterTracking` or `Add<Provider>DeadLetterTrackingClient`), through the same `IDeadLetterService` the Dashboard uses. The actions are mapped only when that service is registered, so a host without dead letter tracking has no such routes. Dead letters live in the shared tracking database, so any instance that has the service can answer.

| Action | Route | Authorization | PowerShell cmdlet |
|---|---|---|---|
| List dead letters | `GET {BasePath}/dead-letters?queue=&subscription=&status=&messageId=&skip=&take=` | Read | `Get-FlowlyDeadLetter` |
| Requeue a dead letter | `POST {BasePath}/dead-letters/requeue` with `{ "messageId", "requeuedBy", "messageBody" }` (`requeuedBy` and `messageBody` optional) | Write | `Submit-FlowlyDeadLetter` |
| Discard a dead letter | `POST {BasePath}/dead-letters/discard` with `{ "messageId" }` | Write | `Remove-FlowlyDeadLetter` |

```powershell
Get-FlowlyDeadLetter -Status Pending -QueueName my-queue
Get-FlowlyDeadLetter -MessageId $id | Submit-FlowlyDeadLetter -RequeuedBy "ops@example.com"
Submit-FlowlyDeadLetter -MessageId $id -MessageBody '{"orderId":42,"amount":12.5}'
Get-FlowlyDeadLetter -Status Pending -First 20 | Remove-FlowlyDeadLetter
```

The list is newest first and filtered by `queue` (the topic name for an event subscription dead letter), `subscription`, `status` (`Pending` or `Requeued`) and `messageId`; `skip` and `take` (1 to 1000) page through it, and without `take` everything after `skip` is returned. Requeue stores `requeuedBy` as the audit trail; when it is omitted or blank the API records the authenticated caller's identity instead, never a value taken from a request header: the principal's name for host authorization policies, the principal's name, then the `sub` or `azp` claim, for OAuth2 (an Entra client credentials token has no name, so its `sub` or `azp` is stored), and nothing for an API key, which carries no identity. An explicit `requeuedBy` overrides the default. Requeue and discard answer `204`, `404` for an unknown message id, `409` when the dead letter is not `Pending` (a requeued dead letter can be neither requeued nor discarded again), `400` for a blank message id or a rejected `messageBody`. Discarding permanently deletes the record. The optional `messageBody` (`-MessageBody`) republishes a corrected body instead of the stored one; see [Fix and requeue](#fix-and-requeue-a-dead-letter). The Write cmdlets ask for confirmation (`-WhatIf` and `-Confirm` are supported). See [`src/Flowly.ManagementApi/README.md`](src/Flowly.ManagementApi/README.md) for the full contract.

---

## Job Tracking

For long-running work where you need to track status, query progress, or detect failures, use job handlers.

### Define a job message

Job messages must implement `IJobMessage`:

```csharp
public record ProcessReportJob(Guid ReportId, DateOnly Period) : IJobMessage
{
    public string Description => $"Process report {ReportId}";
    public string JobTypeName => nameof(ProcessReportJob);
}
```

### Write a job handler

```csharp
[RetryPolicy(maxRetries: 2, delaySeconds: 120)]
public class ProcessReportJobHandler : JobHandler<ProcessReportJob>
{
    public override async Task Handle(IJobMessageContext<ProcessReportJob> ctx)
    {
        var job   = ctx.Message;
        var jobId = ctx.JobId;
        var ct    = ctx.CancellationToken;

        await ctx.SaveState(new { Step = "Fetching data" });

        var data = await FetchData(job.ReportId, ct);

        await ctx.SaveState(new { Step = "Generating PDF", Rows = data.Count });

        await GeneratePdf(data, ct);
    }
}
```

Register:

```csharp
builder.AddJobHandler<ProcessReportJob, ProcessReportJobHandler>();
```

### Submit a job

```csharp
builder.AddJobSubmitter<ProcessReportJob>();
```

```csharp
public class ReportController(IJobMessageSender jobSender)
{
    public async Task<JobId> StartReport(DateOnly period)
    {
        var jobId = await jobSender.QueueJob(new ProcessReportJob(Guid.NewGuid(), period));
        return jobId; // poll this ID to check status
    }
}
```

### Job lifecycle

```
Created → Started → Completed
                 → Failed
```

During retries the job remains in `Started`. The `RetryAttempt` field on the job record increments with each attempt. A heartbeat signal is sent every 30 seconds while the handler runs; jobs with no heartbeat for >30 minutes are automatically marked `Failed`.

### Enable job state persistence

```csharp
builder.AddSqlServerJobStateTracking("JobsDb");
// or
builder.AddPostgresJobStateTracking("JobsDb");
// or
builder.AddSQLiteJobStateTracking("Data Source=jobs.db");
```

All three run EF Core migrations at startup by default (`enableMigrations: true`).

---

## Recurring Jobs

For scheduled background work — nightly reports, cleanup tasks, data syncs.

```csharp
[RecurringJob("Nightly Report", "0 2 * * *")]   // runs at 02:00 every day
public class NightlyReportJob : RecurringJobHandler
{
    public override async Task Handle(CancellationToken ct)
    {
        await GenerateReport(ct);
    }
}
```

Register:

```csharp
builder.AddRecurringJob<NightlyReportJob>();
```

### CRON expressions

Flowly uses the [Cronos](https://github.com/HangfireIO/Cronos) library. Both 5-field (standard) and 6-field (with seconds) expressions are supported.

```
"0 2 * * *"       → 02:00 every day
"0 */6 * * *"     → every 6 hours
"*/30 * * * * *"  → every 30 seconds (6-field)
```

### Execution guarantees

The scheduler polls every 5 seconds and submits a trigger message when a job is due. Execution uses session-based queues so only one instance of each recurring job runs at a time, even across multiple service replicas.

### Job state integration

If `AddJobStateTracking` is configured, recurring jobs are tracked in the database alongside regular jobs. Recurring jobs do not support retry or dead letter tracking — if a run fails, the next CRON tick triggers a new attempt.

---

## Local Development

### .NET Aspire (recommended)

The `Flowly.AzureServiceBus.Aspire` package integrates with the Azure Service Bus emulator in .NET Aspire AppHost projects. It discovers and registers all queues from your service's `FlowlyConfiguration` automatically.

In your AppHost:

```csharp
var azureServiceBus = builder
    .AddAzureServiceBus("EmulatorNamespace")
    .RunAsEmulator(emulator => emulator.WithConfiguration("servicebus-config.json"));

var backendProcessor = builder.AddProject<Projects.BackendProcessor>("BackendProcessor");

// Auto-discovers queues and events from the project's FlowlyConfiguration
azureServiceBus.AddFlowly(backendProcessor);

backendProcessor
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);
```

When a service uses inline Flowly configuration (no `FlowlyConfiguration` class), there is no design-time class to discover — declare the topology explicitly instead:

```csharp
var backendFinanceProcessor = builder.AddProject<Projects.BackendFinanceProcessor>("BackendFinanceProcessor");

// Explicit topology for services that use inline AddFlowly() configuration
azureServiceBus.AddFlowly(backendFinanceProcessor, topology =>
    topology.AddEventSubscription<OrderProcessedEvent>("finance-order-processed-event-handler"));

backendFinanceProcessor
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);
```

`IFlowlyAspireTopologyBuilder` supports `.AddQueue(name)` and `.AddEventSubscription<TEvent>(subscriptionName)`. The topic name for `AddEventSubscription` is derived from the event type the same way as at runtime.

**RPC call handlers:** When a sender uses `AddCallSubmitter<TMessage>()` it owns a reply queue named `{callQueue}.reply.{InstanceName}`. The emulator must have this queue pre-created, so call `AddFlowly` for the sender project as well and pass the `instanceName` that matches `FlowlyOptions.InstanceName` in the sender's `Program.cs`:

```csharp
// AppHost Program.cs
azureServiceBus.AddFlowly(receiver);                         // registers the main queue
azureServiceBus.AddFlowly(sender, instanceName: "sender");   // registers the reply queue
```

Reference `Flowly.AzureServiceBus.Aspire` in the AppHost `.csproj` with `IsAspireProjectResource="false"`:

```xml
<ProjectReference Include="..\..\Flowly.AzureServiceBus.Aspire\Flowly.AzureServiceBus.Aspire.csproj"
                  IsAspireProjectResource="false" />
```

### Docker Compose

Use `flowly docker-compose` to generate a `docker-compose.yml` that includes the right local infrastructure for your project — RabbitMQ, the Azure Service Bus emulator, or both, depending on which transport packages are referenced:

```bash
flowly docker-compose --project ./Sender --project ./Receiver --output docker-compose.yml
```

For multi-service solutions, pass multiple `--project` flags. The tool detects all transports and database providers across every project and generates a single composed file. Then start everything with:

```bash
docker compose up -d
```

When Azure Service Bus is detected, the tool automatically generates `sbconfig.json` alongside `docker-compose.yml` and configures the emulator to mount it. You can also write to stdout and pipe it yourself:

```bash
flowly docker-compose --project ./Sender --project ./Receiver > docker-compose.yml
```

For the Azure Service Bus emulator specifically, you can also generate the queue configuration file independently:

```bash
flowly azure-service-bus emulator-config \
  --project ./MyService \
  --namespace EmulatorNamespace \
  --output ./servicebus-config.json
```

---

## Flowly.Tool CLI

The `flowly` CLI tool operates on your service project at design time. It loads your `FlowlyConfiguration` subclass from the built assembly to discover queue topology.

### Install

```bash
dotnet tool install --global Flowly.Tool
```

To update to a newer version:

```bash
dotnet tool update --global Flowly.Tool
```

To uninstall:

```bash
dotnet tool uninstall --global Flowly.Tool
```

### Commands

```bash
# Generate docker-compose.yml with all local development dependencies
flowly docker-compose --project ./Sender --project ./Receiver --output docker-compose.yml

# Or pipe to stdout
flowly docker-compose --project ./Sender --project ./Receiver > docker-compose.yml

# List all queues a project registers
flowly azure-service-bus queues --project ./MyService

# Generate Azure Service Bus emulator config JSON
flowly azure-service-bus emulator-config \
  --project ./MyService \
  --namespace EmulatorNamespace \
  --output ./servicebus-config.json

# Generate Bicep IaC for queue provisioning
flowly azure-service-bus bicep \
  --project ./MyService \
  --service-bus-namespace-name sb-myapp \
  --output ./queues.bicep

# Generate Aspire AppHost bootstrap code
flowly azure-service-bus aspire-code \
  --project ./MyService \
  --connection-name EmulatorNamespace \
  --output ./aspire-bootstrap.cs
```

Pass multiple `--project` flags to aggregate queues across several services into a single output file.

#### `docker-compose` options

| Option | Description |
|---|---|
| `--project` / `-p` | Path to a `.csproj` or folder. Repeat for multiple projects. |
| `--output` / `-o` | Write `docker-compose.yml` to this path. Defaults to stdout. |
| `--namespace` | ASB emulator namespace name (default: `sbemulatorns`). |
| `--sbconfig-output` | Override the path for the generated `sbconfig.json` (ASB only). |

The tool detects transports from package references (`Flowly.AzureServiceBus.dll`, `Flowly.RabbitMQ.dll`) and database providers from (`Flowly.Jobs.SqlServer.dll`, `Flowly.DeadLetters.Postgres.dll`, `Flowly.MessageStreams.SqlServer.dll`, `Flowly.MessageStreams.Postgres.dll`, etc.) in the build output. No configuration class is required — it works with both inline `AddFlowly()` and `FlowlyDesignTimeFactory` setups.

---

## Project Templates

Scaffold new Flowly projects in seconds using `dotnet new`:

### Install

```bash
dotnet new install Flowly.Templates
```

### `flowlyaspireapp` — Scaffold a complete Aspire-based send/receive solution

Generates a full .NET Aspire solution — AppHost, ServiceDefaults, Messages, Sender, and Receiver — wired for your chosen transport. OpenTelemetry is always enabled. Aspire provisions all infrastructure; no docker-compose or sbconfig.json required.

```bash
dotnet new flowlyaspireapp --transport <rabbitmq|asb|eventhubs|inmemory> [options] -n <SolutionName>
```

Supports the same `--callhandler`, `--streamhandler`, `--partitions`, `--stream-checkpoint`, `--jobs`, `--deadletter`, `--db`, and `--dashboard` flags as `flowlyapp`, with the same architecture: `--jobs` scaffolds a dedicated `JobTracker` project and `--deadletter` scaffolds a dedicated `DeadLetterTracker` project — the Receiver stays a pure message-processing worker. For RabbitMQ, `--partitions` also makes the AppHost bind-mount the plugin/config files and pin the Stream protocol port to `5552` on the RabbitMQ container, mirroring what `flowlyapp`'s `docker-compose.yml` does. `--stream-checkpoint` (SQL Server/PostgreSQL) provisions a `MsgStreamDb` AppHost database off the same resource `--db` already provisions when the engine matches — SQLite gets a static connection string instead, no AppHost resource needed. `--transport eventhubs` (alias `eh`) scaffolds an Azure Event Hubs stream (always the `MessageStreamHandler<T>`/`IMessageRecorder` pair, so `--stream` is implied; `--call`, `--jobs` and `--deadletter` are rejected because Event Hubs hosts streams only): the AppHost uses Aspire's Azure Event Hubs and Azure Storage hosting integrations to run the Event Hubs emulator and Azurite, declares the event hub (with `--partitions` as its partition count, one partition otherwise), its consumer group, and the blob service the Receiver uses for partition ownership. Run everything with:

```bash
dotnet run --project MyApp.AppHost
```

The AppHost sets `<AspireUseCliBundle>true</AspireUseCliBundle>` (the default for new Aspire apps), so `dotnet run` is handed to the Aspire CLI (`aspire run`), which supplies the dashboard and orchestrator. You do not need to install anything beyond the .NET 10 SDK: if `aspire` is not on your PATH, the SDK's `dnx` fetches the matching `Aspire.Cli` package from NuGet on first run (network access required, can take a minute). A solution-root `aspire.config.json` points the CLI at the AppHost project. Set `AspireUseCliBundle` to `false` in the AppHost `.csproj` to opt out (the build then emits warning `ASPIRE010`; add `<NoWarn>$(NoWarn);ASPIRE010</NoWarn>` to silence it).

---

### `flowlyapp` — Scaffold a complete send/receive solution

The fastest way to get a working Flowly app running locally. Generates a full solution — Messages contracts library, Sender, and Receiver — matching the quickstart guides exactly. Includes `docker-compose.yml` for local infrastructure and `sbconfig.json` for Azure Service Bus.

```bash
dotnet new flowlyapp --transport <transport> [options] -n <SolutionName>
```

`--transport` is required:

| Value | Alias | Transport |
|---|---|---|
| `rabbitmq` | `rmq` | RabbitMQ |
| `azureservicebus` | `asb` | Azure Service Bus |
| `eventhubs` | `eh` | Azure Event Hubs (message streams only; implies `--stream`) |
| `inmemory` | `inm` | In-Memory (no broker) |

Optional features:

| Flag | Alias | Description |
|---|---|---|
| `--callhandler` | `--call` | Scaffold as RPC-style call/response — `MyMessage` implements `IReturns<MyReturnMessage>`; the sender uses `IMessageCaller.Call` and blocks for the response. Rejected with `--transport eventhubs`. |
| `--streamhandler` | `--stream` | Scaffold the main message as an append-only, replayable stream — `MyMessage` gets `[StreamRetention]`, the Receiver's handler becomes a `MessageStreamHandler<T>`, and the sender uses `IMessageRecorder.Record`. RabbitMQ, InMemory or Azure Event Hubs (always on for `--transport eventhubs`); incompatible with `--call`. |
| `--partitions <n>` | | Partition the stream into `n` independent, ordered sub-logs via `[StreamPartitions(n)]`. `0` (default) = non-partitioned. Requires `--stream`. On RabbitMQ, the generated `docker-compose.yml` also publishes the Stream protocol port (`5552`) and enables the `rabbitmq_stream` plugin needed for partitioned consumption. On Azure Event Hubs, the generated `ehconfig.json` declares the event hub with exactly `n` partitions (one partition when not used), because Flowly never creates event hubs and requires the partition ids to match the contract. |
| `--stream-checkpoint <value>` | | Add built-in message stream checkpoint persistence (`sqlserver`, `postgres`, or `sqlite`) — wires `Add<Provider>MessageStreamPersistence("MsgStreamDb")` and `UseMessageStreamCheckpoint<T>()` into the Receiver, plus a matching connection string and (SQL Server/PostgreSQL) `docker-compose.yml` container, reused when `--db` already provisions the same engine. Requires `--stream` and `--transport rabbitmq` or `--transport eventhubs` (where `--stream` is implied) — silently has no effect otherwise (not on InMemory), the same way `--db` has no effect without `--jobs`/`--deadletter`. |
| `--jobtracking` | `--jobs` | Job state tracking — adds `ProcessJobMessage`, `ProcessJobHandler`, `JobSubmitterService`, and a `JobTracker` infrastructure project. Requires a DB flag. Rejected with `--transport eventhubs`. |
| `--deadlettertracking` | `--deadletter` | Dead-letter tracking — adds `DeadLetterSampleMessage`, `DeadLetterSampleMessageHandler` with `[RetryPolicy]`, and `FailingMessageSenderService`. Requires a DB flag. Rejected with `--transport eventhubs`. |
| `--opentelemetry` | `--otel` | Add Flowly.OpenTelemetry instrumentation. No exporter — signals are collected but not emitted unless `--otel-export` is also specified. |
| `--otel-export <value>` | `--oe` | Add Flowly.OpenTelemetry instrumentation **and** wire an exporter. Values: `default` (OTLP, gated on `OTEL_EXPORTER_OTLP_ENDPOINT`), `jaeger` (OTLP unconditional + Jaeger in docker-compose), `zipkin` (Zipkin + Zipkin in docker-compose). Implies `--otel`. |
| `--dashboard` | | Scaffold a standalone `Dashboard/` project hosting the Flowly management UI at `/`. Its port is randomly assigned per instantiation (HTTP 5000–5300, HTTPS 7000–7300 for `flowlyapp`; HTTP 5400–5499, HTTPS 7400–7499 for `flowlyaspireapp`) — see `Dashboard/Properties/launchSettings.json`. For InMemory transport the dashboard is embedded in `App/` instead. |

Database backend (required when `--jobs` or `--deadletter` is used): `--db sqlserver|postgres|sqlite`.

```bash
# Full RabbitMQ solution
dotnet new flowlyapp --transport rabbitmq -n MyApp

# Full Azure Service Bus solution (includes sbconfig.json for the emulator)
dotnet new flowlyapp --transport asb -n MyApp

# Single-project InMemory solution (no broker, no Docker required)
dotnet new flowlyapp --transport inm -n MyApp

# RPC-style call/response (RabbitMQ)
dotnet new flowlyapp --transport rabbitmq --call -n MyApp

# Partitioned message stream (RabbitMQ, scales across instances)
dotnet new flowlyapp --transport rabbitmq --stream --partitions 4 -n MyApp

# Message stream with built-in checkpoint persistence (RabbitMQ, SQLite)
dotnet new flowlyapp --transport rabbitmq --stream --stream-checkpoint sqlite -n MyApp

# Partitioned message stream on Azure Event Hubs with checkpoint persistence (emulator + Azurite in docker-compose)
dotnet new flowlyapp --transport eventhubs --partitions 4 --stream-checkpoint sqlite -n MyApp

# RabbitMQ with job tracking and dead-letter tracking (SQLite)
dotnet new flowlyapp --transport rabbitmq --jobs --deadletter --db sqlite -n MyApp

# ASB with job tracking using SQL Server
dotnet new flowlyapp --transport asb --jobs --db sqlserver -n MyApp

# RabbitMQ with Jaeger tracing (adds Jaeger to docker-compose, sets OTLP endpoint in launchSettings)
dotnet new flowlyapp --transport rabbitmq --otel-export jaeger -n MyApp

# RabbitMQ with standalone Dashboard project
dotnet new flowlyapp --transport rabbitmq --dashboard -n MyApp
```

**RabbitMQ / Azure Service Bus** generates:

```
MyApp/
├── MyApp.slnx
├── Messages/            ← shared message contracts
├── Sender/              ← WebApplication; sends messages
├── Receiver/            ← worker; receives and handles messages
├── JobTracker/          ← only with --jobs: infrastructure service for job state persistence
├── Dashboard/           ← only with --dashboard: standalone web app hosting the management UI at /flowly
├── docker-compose.yml   ← broker (+ SQL Server / Postgres when applicable)
└── sbconfig.json        ← ASB only
```

**Azure Event Hubs** generates the same `Messages`/`Sender`/`Receiver` solution as a RabbitMQ stream, plus a `docker-compose.yml` with the Event Hubs emulator and Azurite and an `ehconfig.json` that declares the event hub `my`, its partition count and the consumer group `MyMessageHandler` (the emulator has no management plane and Flowly never creates event hubs). The Receiver is registered with a blob container on Azurite for partition ownership, so a second Receiver instance shares the partitions. The compose file sets `ACCEPT_EULA=Y` on the emulator, which accepts its license terms on your behalf, so `docker compose up -d` works as is.

**InMemory** generates a single-project solution (`App/`) with sender, receiver, and optionally job/dead-letter tracking in the same process. With `--dashboard`, the management UI is embedded in `App/`.

---

### `flowly` — Scaffold a new Flowly project

```bash
dotnet new flowly --transport <transport> [options] -o <ProjectName>
```

`--transport` is required. Accepted values:

| Value | Alias | Transport |
|---|---|---|
| `rabbitmq` | `rmq` | RabbitMQ |
| `azureservicebus` | `asb` | Azure Service Bus |
| `inmemory` | `inm` | In-Memory (no broker) |

Optional feature flags:

| Flag | Alias | Description |
|---|---|---|
| `--jobtracking` | `--jobs` | Add job state tracking. Requires a DB flag. |
| `--deadlettertracking` | `--deadletter` | Add dead-letter tracking. Requires a DB flag. |
| `--opentelemetry` | `--otel` | Add Flowly.OpenTelemetry instrumentation. No exporter wired — use `--otel-export` to also emit signals. |
| `--otel-export <value>` | `--oe` | Add Flowly.OpenTelemetry instrumentation **and** wire an exporter. Values: `default` (OTLP, gated on `OTEL_EXPORTER_OTLP_ENDPOINT`), `jaeger` (OTLP unconditional, sets endpoint in launchSettings), `zipkin` (Zipkin exporter). Implies `--otel`. |
| `--inline` | | Wire Flowly inline in Program.cs instead of a config class. |
| `--no-http` | | Configure as a worker service with no HTTP listener. Use for projects that only process queue messages. |

Database backend (required when `--jobs` or `--deadletter` is used):

| Value | Database |
|---|---|
| `--db sqlserver` | SQL Server |
| `--db postgres` | PostgreSQL |
| `--db sqlite` | SQLite |

#### Examples

```bash
# Minimal RabbitMQ receiver
dotnet new flowly --transport rabbitmq -o Receiver

# Queue-only worker (no HTTP listener)
dotnet new flowly --transport rabbitmq --no-http -o Worker

# Azure Service Bus processor with job tracking, dead-letter tracking, and OTel
dotnet new flowly --transport asb --jobs --db sqlserver --deadletter --otel -o Processor

# RabbitMQ receiver with Jaeger export (OTLP wired, endpoint set in launchSettings)
dotnet new flowly --transport rabbitmq --otel-export jaeger -o Receiver

# InMemory transport, all features, inline wiring
dotnet new flowly --transport inm --jobs --deadletter --db sqlite --otel --inline -o TestWorker
```

### `flowlymessagelib` — Scaffold a Flowly message contracts library

Creates a class library pre-wired with Flowly for holding shared message contracts. Reference this project from both your sender and receiver services.

```bash
dotnet new flowlymessagelib -o <ProjectName>
```

| Flag | Alias | Description |
|------|-------|-------------|
| `--jobtracking` | `--jobs` | Add a `Flowly.Jobs` dependency and a `MyJobMessage.cs` starter file. |

### `flowlyskills` — Install Flowly AI agent skills

Drops Flowly AI skills for [Claude Code](https://claude.ai/code) or [GitHub Copilot CLI](https://docs.github.com/en/copilot/concepts/agents/about-agent-skills) into the current directory. Run this from your repository root so skills are available across all projects in the repo. No project name required.

```bash
dotnet new flowlyskills                      # Claude Code (default) -> .claude/skills/
dotnet new flowlyskills --agent copilot      # GitHub Copilot CLI    -> .github/skills/
```

| Option | Values | Default | Description |
|---|---|---|---|
| `--agent` | `claude`, `copilot` | `claude` | The AI coding agent to install the skills for. `claude` writes `.claude/skills/` (Claude Code); `copilot` writes `.github/skills/` (GitHub Copilot CLI's own project skills folder). |

The skills are identical for both agents; only the target folder differs. GitHub Copilot CLI also discovers project skills in `.claude/skills/`, so the default install works with both tools; use `--agent copilot` to keep skills under `.github/` instead. Install into `.claude/skills/` and `.github/skills/` at the same time only if you want both folders, since a skill present in both is a duplicate. The skills teach the agent how to scaffold message handlers, handler steps and send steps, recurring jobs, contracts assemblies, and configure Flowly transports. A few skills use Claude Code specifics (the `arguments` frontmatter field and the `AskUserQuestion` tool); GitHub Copilot CLI ignores the field and asks in chat instead.

## Full Configuration Example

```csharp
public class MyServiceConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            // Transport
            .UseAzureServiceBus("AzureServiceBus")

            // Job state tracking (SQL Server)
            .AddSqlServerJobStateTracking("Jobs")

            // Dead letter tracking (SQL Server)
            .AddSqlServerDeadLetterTracking("DeadLetters")

            // Regular handler with retry and dead letter tracking
            .AddMessageHandler<OrderCreated, OrderCreatedHandler>()
            .WithDeadLetterTracking()

            // Regular handler — retries only, no DLQ tracking
            .AddMessageHandler<InvoiceGenerated, InvoiceGeneratedHandler>()

            // Batch handler
            .AddBatchMessageHandler<AnalyticsEvent, AnalyticsEventBatchHandler>()

            // Job handler with retry
            .AddJobHandler<ProcessReportJob, ProcessReportJobHandler>()

            // Recurring jobs
            .AddRecurringJob<NightlyReportJob>()
            .AddRecurringJob<CleanupOldRecordsJob>()

            // Submitters
            .AddMessageSubmitter<OrderCreated>()
            .AddJobSubmitter<ProcessReportJob>()

            // Events (fan-out)
            .AddEventHandler<OrderCompleted, OrderCompletedEventHandler>()
            .AddEventSubmitter<OrderCompleted>();
    }
}
```

```csharp
// Handler with all options
[MaxConcurrentCalls(5)]
[DefaultMessageTimeToLive("2.00:00:00")]
[LockDuration("00:10:00")]
[RetryPolicy(maxRetries: 3, delaySeconds: 60)]
public class OrderCreatedHandler : MessageHandler<OrderCreated>
{
    private readonly IOrderRepository _orders;

    public OrderCreatedHandler(IOrderRepository orders) => _orders = orders;

    public override async Task Handle(IMessageContext<OrderCreated> ctx)
    {
        await _orders.Save(ctx.Message, ctx.CancellationToken);
    }
}
```

---

## Multi-Provider

Flowly supports running multiple message brokers in the same service. The first registered provider may be left unnamed and gets its transport's default name; every later provider, of any transport, needs an explicit `name`, otherwise registration throws. The first positional argument of `UseAzureServiceBus`, `UseRabbitMq` and `UseAzureEventHubs` is the connection (a connection string or a connection string name), not the provider name, so pass the name as `name:`:

```csharp
builder
    .UseAzureServiceBus("AzureServiceBus")   // first registered — receives messages and events with no explicit affinity
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>()

    .UseRabbitMq("RabbitMQ", name: "Rabbit")  // second — needs a name; also receives stream contracts with no explicit affinity
    .AddMessageHandler<AnalyticsEvent, AnalyticsEventHandler>();
```

A message type without an attribute is routed per capability (queue message, event, stream) to the first registered provider that supports that capability. Pin a message type to a specific provider by annotating its message class:

```csharp
[ProviderAffinity("Rabbit")]
public record AnalyticsEvent(Guid UserId, string EventName);
```

The string in `[ProviderAffinity]` must match the `name` given at registration (`"Rabbit"` above).

At startup, Flowly validates cross-provider topology consistency:

- **Same transport type + same queue name + conflicting settings** → throws `InvalidOperationException`
- **Different transport types + same queue name** → logs a warning and continues
- **Same queue name + identical settings** → allowed silently

Providers also differ in what they support: Azure Service Bus offers queues and events, RabbitMQ and InMemory offer queues, events and message streams, and Azure Event Hubs offers message streams only (`IMessageRecorder` and `MessageStreamHandler<T>`). Routing takes that into account. With Azure Service Bus registered first and RabbitMQ second, a stream contract without `[ProviderAffinity]` goes to RabbitMQ, and everything else stays on Azure Service Bus. A transport that only supports streams, such as Azure Event Hubs, is never chosen for queue messages or events, whatever order the providers are registered in. The *primary provider* — the fallback for recurring jobs, dead letter requeueing and the primary-provider queue discovery in the Aspire integration and the CLI — is the first registered provider that supports queues. Registering a handler or submitter that no provider can host, or pinning one with `[ProviderAffinity]` to a provider that cannot host it (for example a `MessageHandler<T>` against a transport that only supports streams), throws at registration time with a message naming the contract, the capability and the registered providers.

See **[Multi-Provider Configuration](docs/multi-provider.md)** for routing rules, all supported scenarios, and the full startup validation reference.

---

## Azure Service Bus Transport

Pass `enableHealthCheck: true` to register a health check under the tag `"azure-service-bus"`:

```csharp
builder.UseAzureServiceBus("AzureServiceBus", enableHealthCheck: true);
```

Managed identity is supported by passing a `TokenCredential` instead of a connection string:

```csharp
builder.UseAzureServiceBus("sb-myapp.servicebus.windows.net", new DefaultAzureCredential());
```

---

## RabbitMQ Transport

### Registration

```csharp
builder.UseRabbitMq("RabbitMQ")   // connection string name in appsettings
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>();
```

The default connection string is `amqp://guest:guest@localhost:5672/`. Pass a configuration key or a literal AMQP URI. A single positional string is always the connection, as for the other transports; the provider name is only set with `name:`, for example `UseRabbitMq("RabbitMQ", name: "Rabbit")`.

Pass `enableHealthCheck: true` to register a health check under the tag `"rabbitmq"`:

```csharp
builder.UseRabbitMq("RabbitMQ", enableHealthCheck: true);
```

### Retry topology and `createTopology`

Flowly's retry mechanism for RabbitMQ works by publishing the retried message to a `{queue}.retry` queue with a per-message TTL. When the TTL expires, RabbitMQ's Dead Letter Exchange (DLX) routes the message back to the main queue. This requires the retry queue to be declared with specific arguments:

| Argument | Value |
|---|---|
| `x-dead-letter-exchange` | `""` (default exchange) |
| `x-dead-letter-routing-key` | `{queue}` (the main queue name) |

By default (`createTopology: true`), Flowly creates the full queue topology — including the `.retry` queue, `.dlx` exchange, and `.dead-letter` queue — at startup. No manual configuration is required.

When `createTopology: false`, you are responsible for provisioning this topology before starting the application. **Flowly validates the retry queues at startup** and throws `InvalidOperationException` if any `{queue}.retry` queue is missing:

```
RabbitMQ retry queue 'order-created.retry' does not exist.
When createTopology is false, the retry queue must be pre-declared with
x-dead-letter-exchange="" and x-dead-letter-routing-key="order-created".
Either set createTopology: true or ensure the queue topology is provisioned before startup.
```

> **Important:** The startup check confirms that the retry queue *exists*, but cannot verify that the DLX arguments are set correctly. If the queue was declared without the correct `x-dead-letter-exchange` and `x-dead-letter-routing-key` arguments, retried messages will expire silently without being re-routed. Always use the exact arguments listed above.

---

## Azure Event Hubs Transport

`Flowly.AzureEventHubs` is a stream-only transport: it hosts `IMessageRecorder`, `MessageStreamHandler<T>` and `[StreamPartitions]` and nothing queue-based. Registering a message handler, submitter, call handler, job, event or dead letter source against it throws at startup.

```csharp
builder
    .UseAzureEventHubs("EventHubs")                    // connection string name; no EntityPath in the connection string
    .AddMessageRecorder<TelemetryReading>()
    .AddMessageStreamHandler<TelemetryReading, TelemetryStreamHandler>();
```

Use Microsoft Entra ID with `UseAzureEventHubs("my-namespace.servicebus.windows.net", new DefaultAzureCredential())`. The registration takes flat optional parameters: `name`, `partitionOwnershipContainer` (an existing `BlobContainerClient` that lets several instances share partition ownership; not needed for a single instance), `loadBalancingInterval` and `partitionOwnershipExpiration` (how often the instances sharing a consumer group renew and claim partitions, and after how long an unrenewed partition is considered abandoned; the SDK defaults of 30 seconds and 2 minutes apply when omitted, both must be greater than zero and the expiration must be greater than the interval), `maxMessageSizeBytes` and `enableHealthCheck`. There is no `createTopology` parameter.

- **Health check.** `enableHealthCheck: true` registers a health check named `azure-event-hubs` (`azure-event-hubs-<name>` for another provider name), tagged `"azure-event-hubs"`, that reads the properties of every event hub registered on the provider and so shows that the connection, the credential and the permission to read event hub properties work. A failing probe, a missing event hub or a probe that takes longer than 5 seconds makes it `Unhealthy` (naming the event hubs), there is no `Degraded` result and the result is not cached. It is independent of `AddMessageStreamHealthCheck()`, which reports halted and waiting partitions, see [Partition state and health](#partition-state-and-health).

- **The event hub is the stream.** Its name is the stream queue name resolved for the message contract (`[QueueName]` overrides it). Flowly never creates event hubs: topology creation is always off for this provider (it registers no queue topology creator), and at startup Flowly validates that each event hub exists, that its partition ids are `0` to `n-1` for `[StreamPartitions(n)]` (exactly `0` without `[StreamPartitions]`), and that the partition ownership container exists when one is configured. A failed check stops the host, also when the host asked for `createTopology: true`. There is no reconciliation and changing the partition count of an event hub while the application runs is not supported.
- **The consumer group is the handler's `ConsumerName`.** It defaults to the handler type name, and the consumer group must already exist on the event hub: create it, or set `ConsumerName = "$Default"` to read through the default consumer group. A `ConsumerName` that is not a valid consumer group name (1 to 50 letters, digits, `.`, `-` or `_`, starting and ending with a letter or digit, which excludes the names of generic handler types) fails startup naming it, and so does a consumer group that does not exist. One consumer group is one independent reader: two handlers reading the same event hub need different `ConsumerName` values.
- **Start positions** map to Event Hubs positions: `First()` is the earliest retained event, `Last()` only events enqueued after the handler attaches, `Offset(n)` the event with sequence number `n` (sequence numbers are per partition and start at zero), and `Timestamp(dt)` the first event enqueued at or after the time. A registered [checkpoint](#position-persistence) wins over the configured position: the stored sequence number is the last processed event and reading resumes after it. With a checkpoint, `Last()` and a `Timestamp(...)` later than the last enqueued event are resolved to a sequence number and stored before the first event is read; a `Timestamp(...)` at or before the last enqueued event cannot be resolved without reading events, so that position is stored after the first processed batch instead.
- **Partition ownership.** Without a `partitionOwnershipContainer` a single instance owns every partition in-process and needs no storage; a second process reading the same consumer group without a container is not coordinated with the first: both are assigned every partition, but each of them stops receiving some partitions for periods (observed on the emulator: about one load balancing interval) until the SDK notices and reports the loss, so events can be missed. With a container the instances sharing a consumer group split the partitions through the Azure Event Hubs SDK's own load balancing, and a partition moves to another instance when its owner stops or expires. Use a registered checkpoint store when running several instances: without one, the configured `StartPosition` applies again to every partition an instance takes over. Losing a partition without a container is logged as a warning naming the consumer group, because it can mean another reader is consuming that consumer group. When a partition changes owner the batch that was in flight on the previous owner was not checkpointed and is handled again, so handlers are at-least-once and must be idempotent.
- **Halting.** A stream handler that exhausts its retries halts consumption of that partition. The Event Hubs processor keeps the partition's ownership while its batch callback stays blocked (until the partition is resumed, lost or the host stops), so the partition is not given away because its ownership expired, and other partitions keep running. A halted partition is not exempt from rebalancing: when another instance joins the consumer group and takes its share, the share can include a halted partition, which then resumes from the stored position on the new owner and delivers the poison batch again (nothing is skipped).
- **Partition keys use the Event Hubs service-side hash.** `Record(message, ct, partitionKey)` sends with the Event Hubs partition key: the same key always lands in the same partition and order per key is kept, but the partition number differs from the one RabbitMQ and InMemory pick for the same key. No key means round robin; the key is ignored on a non-partitioned contract.
- **Retention is configured on the event hub.** `[StreamRetention(maxLengthBytes: ...)]` is ignored and logs a warning at startup; `[StreamRetention(maxAgeSeconds: ...)]` is neither applied nor verified and logs an information message.
- **Geo-replicated namespaces.** When an event hub reports geo-replication and a message stream checkpoint store is registered, a warning is logged at startup: Azure documents that after a forced failover the offsets of the same data can differ between regions, so a stored position may no longer be valid afterwards. The hub side is exact: the validator reads `IsGeoReplicationEnabled` for each event hub that has a stream registered on the Event Hubs provider, producer-only hubs included. The checkpoint side is host-wide: it only asks whether any `MessageStreamCheckpoint<T>` is registered anywhere in the host, not whether one belongs to a message type read from that event hub. It can therefore warn when no Event Hubs position is stored, for example when the only checkpoint belongs to a RabbitMQ stream handler while a geo-replicated event hub is also used, or when the host only writes to the geo-replicated event hub with `IMessageRecorder`. It never misses the real case, a checkpoint for a handler that reads a geo-replicated event hub, so it errs towards false positives; it is a single startup warning per event hub and never fails startup. It matters only with a registered checkpoint: without one nothing is stored, so there is nothing to go stale, and with one Azure documents that the offsets of the same data can differ between regions after a forced failover. Flowly does not detect or correct this, and the claim rests on Azure documentation, not on a run against the service.
- **Next to a queue transport** no `[ProviderAffinity]` is needed (see [Multi-Provider](#multi-provider)); give the second provider an explicit name: `.UseAzureServiceBus("AzureServiceBus").UseAzureEventHubs("EventHubs", name: "events")`.
- **Project templates.** `dotnet new flowlyapp --transport eventhubs` and `dotnet new flowlyaspireapp --transport eventhubs` scaffold a stream solution against the Event Hubs emulator (see [Project Templates](#project-templates)); they reject `--call`, `--jobs` and `--deadletter` because this transport hosts streams only.

The consuming and producing behaviour above was run against Microsoft's Event Hubs emulator (with Azurite) and `Azure.Messaging.EventHubs` 5.12.2, including restart and seeding with a checkpoint, halting, two instances sharing a blob container and the startup failures; see the [`Flowly.AzureEventHubs` readme](src/Flowly.AzureEventHubs/README.md#verification) for the scope and the [`MessageStreamWithCheckpoint` sample](samples/AzureEventHubs/MessageStreamWithCheckpoint/README.md). The emulator has no management plane and its own limits, so retention floors, service limits and tiers, Microsoft Entra authentication, geo-replication and real-namespace latencies were not verified and rest on the Azure documentation.

---

## In-Memory Transport

The in-memory transport runs entirely in-process using .NET channels — no broker is required. It is suitable for testing, local development, and lightweight scenarios.

### Registration

```csharp
builder.UseInMemory();
```

### Options

All options are configured via the optional `Action<InMemoryOptions>` parameter:

```csharp
builder.UseInMemory(options =>
{
    options.MaxMessageSizeBytes = 512_000;
    options.ChannelCapacity = 500;
    options.EnableReferencePassing = true;
});
```

| Option | Default | Description |
|---|---|---|
| `MaxMessageSizeBytes` | 1 048 576 (1 MB) | Messages exceeding this size throw `MessageTooLargeException`. Not enforced when `EnableReferencePassing` is `true`. |
| `ChannelCapacity` | 1000 | Bounded channel capacity per queue/topic subscription. Writers block when full, applying back-pressure analogous to a real broker. |
| `EnableReferencePassing` | `false` | Pass messages as object references instead of JSON. See below. |

### Reference passing

When `EnableReferencePassing = true`, the sender stores the original object reference in the envelope and the receiver returns it directly — no JSON serialisation or deserialisation occurs.

This is useful as a **mediator-style starting point**: build your application in-process first, then switch to a real broker by replacing `UseInMemory()` with the appropriate transport call. Handler code does not change.

```csharp
builder.UseInMemory(options => options.EnableReferencePassing = true);
```

Retries and scheduled delivery work normally — the object reference is preserved through the channel and scheduled-delivery paths. `MaxMessageSizeBytes` is not enforced when this option is enabled.

> **Trade-off:** Serialisation fidelity is not tested in this mode. Any discrepancy between the in-memory and production behaviour — for example, a property that does not survive a JSON round-trip — will only surface when the real transport is used. Leave `EnableReferencePassing = false` (the default) when you want to validate serialisation as part of your test suite.

### Message streaming

The InMemory transport also implements `IStreamCapableMessageBusClient` (see [Message Streaming](#message-streaming) above), so `MessageStreamHandler<T>` and `IMessageRecorder` work without RabbitMQ. It's backed by an in-process append-only log (`InMemoryStreamLog`) per stream queue instead of a broker-side stream:

- Every stream handler still gets its own independent full replay from its own `StartPosition` — the same log-style semantics as RabbitMQ, so handler code is portable between the two transports.
- No cross-restart persistence, same as RabbitMQ streams — and, unlike RabbitMQ, no cross-process sharing either: the log only exists in the one process's memory.
- Retention (`[StreamRetention]` / `MaxAgeSeconds` / `MaxLengthBytes`) works the same way, with one exception: `MaxLengthBytes` is silently ignored when `EnableReferencePassing` is `true`, since reference-passed messages are never serialized and have no byte size to account against — only `MaxAgeSeconds` applies in that mode.
- No InMemory-specific default retention cap — an unconfigured stream grows unbounded in process memory, matching RabbitMQ's own "unbounded unless configured" behavior rather than a second default to learn.

This is primarily a local development/testing aid, but it's also a reasonable choice for small, single-instance production deployments where avoiding external broker infrastructure is the point (e.g. a self-hosted app on a home server/NAS or a single container) — not "toy/demo only."

---

## OpenTelemetry

The `Flowly.OpenTelemetry` package wires Flowly's metrics and traces into the OpenTelemetry SDK.

### Setup

The quickest way — registers both metrics and traces in one call:

```csharp
builder.AddFlowlyOpenTelemetry();
```

To compose Flowly into an existing OpenTelemetry pipeline instead:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddFlowlyInstrumentation())
    .WithTracing(tracing => tracing.AddFlowlyInstrumentation());
```

### Metrics

All metrics use the meter name `"Flowly"` and follow the `messaging.*` semantic conventions for their attributes (`messaging.destination.name`, `messaging.system`, etc.).

| Metric | Type | Description |
|---|---|---|
| `flowly.message.handler.received` | Counter | Messages received by regular handlers |
| `flowly.message.handler.succeeded` | Counter | Messages processed successfully |
| `flowly.message.handler.failed` | Counter | Messages that failed processing |
| `flowly.message.handler.retried` | Counter | Messages scheduled for retry |
| `flowly.message.handler.duration` | Histogram (ms) | Processing time per message |
| `flowly.message.submitter.sent` | Counter | Messages sent by submitters |
| `flowly.message.submitter.failed` | Counter | Send failures |
| `flowly.message.submitter.duration` | Histogram (ms) | Send duration |
| `flowly.message.handler.halted` | Counter | Stream handlers that halted after exhausting their in-process retries |
| `flowly.message.stream.partition.state` | Gauge | Current state of each stream partition owned by this process: `0` running, `1` waiting for an unresolved start position, `2` halted. Tagged `handler`, `messaging.destination.name`, `consumer` and `partition` (omitted for a non-partitioned stream); a revoked partition is no longer reported |
| `flowly.message.stream.partition.transitions` | Counter | Stream partition state changes, tagged `handler`, `messaging.destination.name`, `consumer`, `partition` (omitted for a non-partitioned stream) and `state=running\|waiting\|halted` (the state entered) |
| `flowly.message.stream.partition.dropped` | Counter | RabbitMQ partitioned streams: messages dropped because this process had no processing loop for their partition (a waiting partition, or a handover in progress). Tagged `messaging.destination.name`, `consumer` and `partition`. Nothing is lost; a dropped message is delivered again from the stored position |
| `flowly.message.stream.partition.stand_downs` | Counter | RabbitMQ partitioned streams: times this instance kept its consumer closed for a cool-down after 3 consecutive automatic recreations that still left a partition waiting, so the broker could hand the partitions to another instance. Tagged `messaging.destination.name` and `consumer` |
| `flowly.message.checkpoint.saved` | Counter | Message stream checkpoint positions successfully saved (built-in or hand-rolled) |
| `flowly.message.checkpoint.save.duration` | Histogram (ms) | Checkpoint save duration |
| `flowly.message.checkpoint.save.failed` | Counter | Checkpoint save failures |
| `flowly.message.checkpoint.resolved` | Counter | Start position resolutions, tagged `outcome=resumed\|fresh\|seeded\|seed_failed` (a stored position was found; the configured position was used without storing it; the configured position was resolved and stored up front; storing it failed and the configured position was used) |
| `flowly.event.handler.received` | Counter | Events received by event handlers |
| `flowly.event.handler.succeeded` | Counter | Events processed successfully |
| `flowly.event.handler.failed` | Counter | Events that failed processing |
| `flowly.event.handler.retried` | Counter | Events scheduled for retry |
| `flowly.event.handler.duration` | Histogram (ms) | Processing time per event |
| `flowly.event.publisher.raised` | Counter | Events raised |
| `flowly.event.publisher.failed` | Counter | Event publish failures |
| `flowly.event.publisher.duration` | Histogram (ms) | Event publish duration |
| `flowly.deadletter.pending` | Gauge | Pending dead-lettered messages |
| `flowly.deadletter.requeued` | Counter | Dead letters successfully requeued |
| `flowly.deadletter.discarded` | Counter | Dead letters discarded |
| `flowly.deadletter.cleanup.pending_purged` | Counter | Pending dead letters purged by retention cleanup (each purged message also gets its own `flowly.deadletter.discard` span, linked to the original trace, and increments `flowly.deadletter.discarded`) |
| `flowly.deadletter.cleanup.requeued_purged` | Counter | Stale Requeued audit rows purged by retention cleanup |
| `flowly.job.failed` | Gauge | Jobs in the Failed state |
| `flowly.job.running` | Gauge | Jobs in the Started state |

### Traces

Each message or event handled creates a span named `flowly.handle {queueName}` with kind `Consumer`. The span includes `handler`, `messaging.system`, `messaging.destination.name`, `messaging.message.id`, and `messaging.message.conversation_id` attributes.

### Custom tags on spans

Implement `IOpenTelemetryTagsProvider` on a message contract to attach business-level tags (e.g. `order.id`, `customer.id`) to Flowly spans:

```csharp
public record SubmitOrderMessage(string OrderId, string CustomerId) : IOpenTelemetryTagsProvider
{
    public IEnumerable<KeyValuePair<string, object?>> GetOpenTelemetryTags() =>
    [
        new("order.id", OrderId),
        new("customer.id", CustomerId),
    ];
}
```

Flowly applies these tags to both the producer span (`flowly.send`) and the consumer span (`flowly.handle`) for that message type. Tags appear in Jaeger and any other OTel backend — they can be used as search filters to correlate traces by business values.

---

## Samples

Runnable samples covering all transports and key Flowly features are available in the [`samples/`](samples/README.md) directory. Each sample is self-contained and includes setup instructions.

See the **[Samples index](samples/README.md)** for a full overview grouped by transport (Azure Service Bus, RabbitMQ, Azure Event Hubs, InMemory, MultiBus).

---

## Claude Code Skills

Flowly ships with a set of [Claude Code](https://claude.ai/code) skills that give Claude contextual knowledge about Flowly conventions. Each skill is a guided, step-by-step scaffold that produces correct handler structure, registration wiring, naming conventions, and unit tests.

### Available skills

| Skill | Command | What it does |
|---|---|---|
| Setup Azure Service Bus | `/flowly-setup-azure-service-bus` | Adds Flowly with Azure Service Bus to a project — packages, `FlowlyConfiguration`, `Program.cs` wiring, connection strings, and optional extensions |
| Setup Azure Event Hubs | `/flowly-setup-azure-event-hubs` | Adds Flowly with Azure Event Hubs (stream-only) to a project — template-first, or packages, `FlowlyConfiguration`, the event hub and consumer group, partition ownership, the local emulator and Aspire AppHost wiring |
| Create message handler | `/create-message-handler OrderPlacedMessage` | Scaffolds a complete handler: message contract record, handler class, registration snippet, and unit tests |
| Create recurring job | `/create-recurring-job NightlyCleanupHandler` | Scaffolds a `RecurringJobHandler` with `[RecurringJob]` attribute, registration, and unit tests |
| Create handler step | `/create-handler-step AuditStep` | Scaffolds a handler step (receiver) or send step (sender): step class, optional marker interface on the message contract, registration, and recipes for audit, logging scope, validation, idempotency, unit of work and sender-side authorization |
| Create contracts assembly | `/create-contracts-assembly` | Creates a shared message contracts project for solutions where multiple services exchange the same message types |

### Installation

Copy the `.claude/skills/` directory (or individual skill subdirectories) from this repository into your own project:

```bash
# Copy the entire skills directory
cp -r .claude/skills/ /path/to/your-project/.claude/skills/

# Or copy individual skills
cp -r .claude/skills/create-message-handler/ /path/to/your-project/.claude/skills/
cp -r .claude/skills/create-recurring-job/ /path/to/your-project/.claude/skills/
cp -r .claude/skills/create-handler-step/ /path/to/your-project/.claude/skills/
```

Claude Code discovers skills under `.claude/skills/` automatically and makes them available as slash commands in any session within that project.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for how to report bugs and propose features. Unsolicited large PRs may be closed — please open an issue first.

---

## Repository

The canonical source lives in a self-hosted Gitea instance. This GitHub repository is a synchronized mirror — CI runs and NuGet releases are managed through Gitea. The `.gitea/workflows/` directory in this repository reflects the actual release pipeline, published here for transparency.

---

## Status

Flowly is under active development. Azure Service Bus and RabbitMQ transports are supported.
