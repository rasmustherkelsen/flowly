# Multi-Bus Architecture Review

**Branch:** `feature/multi-provider-support`
**Date:** 2026-04-11
**Scope:** Enterprise readiness, security, performance, and design quality

---

## Critical Issues

### 1. No credential abstraction — raw connection strings only

**Files:** `AzureServiceBusRegistration.cs`, `RabbitMqRegistration.cs`

For Azure Service Bus specifically, this blocks the most important enterprise pattern: `DefaultAzureCredential` / managed identity. Every serious Azure deployment avoids connection strings. The library should accept `TokenCredential` and construct `ServiceBusClient(string fullyQualifiedNamespace, TokenCredential credential)`. Hardcoding connection string support as the only path will force enterprises to put secrets in config files.

---

### 2. Distributed tracing parent context is not propagated

**Files:** `HandlerInstrumentation.cs`, `SubmitterInstrumentation.cs`

Activities are created with `default(ActivityContext)` instead of linking to a parent span. This breaks distributed traces — every message appears as an orphaned trace rather than being linked to the producer's span. For enterprises using Jaeger, Zipkin, or Grafana Tempo, this makes the library's telemetry nearly worthless for cross-service debugging. The W3C `traceparent` header (or AMQP `Diagnostic-Id`) should be extracted from incoming message properties and used as the parent context.

---

### 3. Message deserialization throws for null body — no error isolation

**Files:** `ReceivedMessage.cs:11-12`, `RabbitMqReceivedMessage.cs:13-15`

A null or corrupt message body throws `InvalidOperationException` before the handler even runs. This means a poison message will repeatedly crash the background service unless the broker's DLQ mechanism saves it. The handler pipeline should catch deserialization failures separately and dead-letter the message cleanly, never crashing the consumer loop.

---

## High Severity

### 4. RabbitMQ batch receiver uses a 50ms polling loop

**File:** `RabbitMqMessageBusReceiver.cs:23`

Polling with `Task.Delay(50ms)` for every batch cycle when idle burns CPU and adds unnecessary latency. RabbitMQ's client supports push-based delivery via `AsyncEventingBasicConsumer`. The batch model should accumulate pushed messages over a configurable window rather than actively polling.

---

### 5. RabbitMQ retry metadata is lost in batch messages

**File:** `RabbitMqBatchReceivedMessage.cs:15-17`

`RetryCount` is not extracted from batch message headers. This means a retried message entering a batch handler has no retry context — the retry counter resets, making retry policy enforcement impossible for batch scenarios. Even if batch handlers don't support retry themselves, discarding this property silently is misleading.

---

### 6. No connection health checks or recovery surface

**Files:** `RabbitMqLazyConnection.cs`, `ServiceBusMessageHandlerBackgroundServiceBase.cs`

Background service consumers have no liveness probes and no integration with `IHealthChecks`. For Kubernetes deployments, a broken connection will cause the pod to appear healthy while messages queue up silently. The library should expose `IHealthCheck` implementations for each registered transport.

---

### 7. Single RabbitMQ connection shared across all channels

**File:** `RabbitMqLazyConnection.cs`

One `IConnection` instance is shared between all senders and receivers. RabbitMQ's own guidance is to use separate connections for publishing and consuming, and potentially a connection per logical group. Under high load, a single connection becomes a bottleneck and a single point of failure — a channel error can bring down all consumers on that connection.

---

## Medium Severity

### 8. Provider affinity is attribute-only — no programmatic or content-based routing

**Files:** `ProviderAffinityAttribute.cs`, `ProviderNameResolver.cs`

`[ProviderAffinity("RabbitMQ")]` on the message class couples the message contract to infrastructure. A message in a shared contracts library cannot be reasonably annotated with a transport name. The builder API should allow routing rules: `.AddMessageSubmitter<T>(provider: "RabbitMQ")` or a delegate. This is especially important when the same message type needs to go to different providers depending on tenant or environment.

---

### 9. Registration-time topology conflict detection is incomplete

**Files:** `QueueManager.cs`, `ProviderQueueManifest.cs`

Queue setting conflicts throw at startup, but conflicts across providers (two different providers claiming the same queue name with different settings) are not surfaced. In a multi-bus setup, this could silently succeed while producing unexpected behavior.

---

### 10. `__primary__` sentinel value is a leaky implementation detail

**File:** `AzureServiceBusRegistration.cs`

Using the string `"__primary__"` as an internal name for the default transport can appear in error messages, logs, and telemetry tags. If a developer accidentally registers a provider with that literal name, they'd silently shadow the primary. This should be modeled with a typed concept — a `PrimaryTransport` marker — not a magic string.

---

### 11. No message size or payload validation boundary

**Files:** `MessageBusSender.cs`, `RabbitMqMessageBusSender.cs`

Azure Service Bus has a 256 KB / 1 MB message size limit (Standard vs. Premium tier). RabbitMQ has configurable limits. Sending an oversized message produces a broker error that surfaces as an unhandled exception inside the background service. The sender should expose a configurable max-size guard with a clear `MessageTooLargeException` before the broker rejects it.

---

### 12. RabbitMQ retry uses `x-expiration` without guaranteed re-routing

**File:** `RabbitMqMessageBusSender.cs:67-71`

The retry delay is implemented by publishing to a `.retry` queue with a TTL header and relying on DLX to route expired messages back to the main queue. This is a fragile pattern: if the DLX is not configured exactly right (which depends on `RabbitMqMessagingTopologyCreator` being called and not skipped via `createTopology: false`), retried messages silently disappear. This constraint should be documented prominently and ideally validated at startup.

---

### 13. Telemetry meter and histogram are created even when telemetry is disabled

**File:** `HandlerInstrumentation.cs:21`

The `Meter` object is allocated at construction regardless of the enabled flag. This is a minor allocation waste, but for an enterprise library that may have hundreds of handler types, it adds up. The `Lazy<T>` or null-object pattern would be cleaner.

---

## Design & Maintainability

### 14. `IFlowlyBuilder` has no `Remove` or `Replace` — DI container becomes the only escape hatch

**File:** `IFlowlyBuilder.cs`

Enterprises often need to override library registrations for testing or tenant-specific deployments. There's no way to swap out a registered transport without rebuilding the entire configuration. A `ReplaceTransport` or at minimum a hook for post-registration overrides would help.

---

### 15. `FlowlyDesignTimeFactory` is both a config class and a factory — responsibilities overlap

**File:** `FlowlyDesignTimeFactory.cs`

This dual role (`IFlowlyConfiguration` + design-time factory) means production configuration is coupled to EF Core's design-time tooling. In a library used by many teams, this causes confusion: developers inherit from `FlowlyDesignTimeFactory` because they copy the sample, not realizing they've also opted into EF migration scaffolding behavior.

---

### 16. Test coverage gaps in multi-provider paths

**Files:** `Flowly.Tests/`

The currently tested paths are mostly single-provider. There are no tests for:

- Routing a message to the correct provider when both are registered
- Topology creation for each provider in isolation and together
- Retry pipeline across providers
- Lane/session routing for RabbitMQ
- Any RabbitMQ-specific implementation

For an enterprise library, these are the highest-risk paths.

---

### 17. No `CancellationToken` propagation to topology creation

**File:** `QueueRegistrarHostedService.cs`

`CreateTopology()` is called in `StartAsync` but the `CancellationToken` from the host is not plumbed through to individual provider topology creation. If app shutdown is requested during startup (e.g., health check timeout), topology creation may not cancel cleanly.

---

## Summary

| # | Area | Severity | File(s) |
|---|------|----------|---------|
| 1 | Credential abstraction missing | Critical | `AzureServiceBusRegistration.cs` |
| 2 | Trace context not propagated | Critical | `HandlerInstrumentation.cs`, `SubmitterInstrumentation.cs` |
| 3 | Null body crashes consumer | Critical | `ReceivedMessage.cs`, `RabbitMqReceivedMessage.cs` |
| 4 | RabbitMQ batch polling loop | High | `RabbitMqMessageBusReceiver.cs` |
| 5 | Retry count lost in batch | High | `RabbitMqBatchReceivedMessage.cs` |
| 6 | No health checks | High | `RabbitMqLazyConnection.cs`, background services |
| 7 | Single RabbitMQ connection | High | `RabbitMqLazyConnection.cs` |
| 8 | Attribute-only routing | Medium | `ProviderAffinityAttribute.cs` |
| 9 | Cross-provider conflict detection | Medium | `QueueManager.cs` |
| 10 | Magic `__primary__` string | Medium | `AzureServiceBusRegistration.cs` |
| 11 | No message size guards | Medium | Senders |
| 12 | Fragile RabbitMQ retry DLX | Medium | `RabbitMqMessageBusSender.cs` |
| 13 | Meter allocated when disabled | Low | `HandlerInstrumentation.cs` |
| 14 | No transport replace/remove | Design | `IFlowlyBuilder.cs` |
| 15 | Dual responsibility on factory | Design | `FlowlyDesignTimeFactory.cs` |
| 16 | Multi-provider test coverage | Design | `Flowly.Tests/` |
| 17 | CancellationToken not plumbed | Low | `QueueRegistrarHostedService.cs` |

The biggest blockers for enterprise adoption are **#1 (credentials)** and **#2 (trace propagation)** — both are table-stakes for Azure-native deployments and platform teams that enforce distributed tracing. **#3** is a reliability hazard that could silently stall a consumer in production. The rest are hardening concerns that can be addressed iteratively.
