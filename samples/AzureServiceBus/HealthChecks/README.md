# Azure Service Bus — Health Checks

Demonstrates how Flowly's built-in `IHealthCheck` for Azure Service Bus surfaces the namespace reachability via the ASP.NET Core health endpoint. Running in Docker, the `Receiver` container's `/health` endpoint reports `Healthy` when the Service Bus namespace is reachable and `Unhealthy` when it is not — giving Kubernetes a reliable liveness probe.

## Projects

| Project | Purpose |
|---|---|
| `Receiver` | Connects to the ASB emulator via Flowly and exposes `/health` |

## What it demonstrates

- Passing `enableHealthCheck: true` to `UseAzureServiceBus()` registers an `IHealthCheck` for that transport — off by default so existing registrations are unaffected
- The health check verifies the namespace is reachable by calling the Service Bus administration API
- `MapHealthChecks("/health")` exposes the result as an HTTP endpoint suitable for Kubernetes liveness/readiness probes
- The Docker Compose `healthcheck` directive uses this endpoint to determine whether the container is ready

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

## How to run

Run from this directory:

```bash
docker compose up --build
```

Docker Compose starts SQL Server and the ASB emulator, waits for the emulator to become available, then builds and starts the `Receiver` container.

## What to observe

The `receiver` container starts immediately after the emulator container is created, but the emulator takes ~30 seconds to initialise (it waits for SQL Server internally). During that window you can watch the health status change in real time:

```bash
curl http://localhost:8080/health   # returns 503 while emulator is starting
# ... wait ~30 seconds ...
curl http://localhost:8080/health   # returns 200 once emulator is ready
```

You can also watch the Docker-reported health status:

```bash
docker compose ps
```

The `receiver` service transitions from `starting` → `unhealthy` → `healthy` as the emulator becomes reachable. This is the exact signal Kubernetes uses to decide whether a pod is ready to serve traffic.

## Notes

- `CreateTopology = false` because this sample only demonstrates connectivity — no message handlers or queues are needed.
- The `sbconfig.json` declares the emulator namespace with no queues since this sample does not process messages.
- Pass `enableHealthCheck: true` to `UseAzureServiceBus()` to opt in. The default is `false` so existing registrations gain no new behaviour.
- For multiple ASB providers (multi-bus), each call independently controls whether its health check is registered. Named providers get distinct check names (`azure-service-bus`, `azure-service-bus-{name}`).
