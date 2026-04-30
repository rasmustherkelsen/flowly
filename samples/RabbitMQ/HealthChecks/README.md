# RabbitMQ — Health Checks

Demonstrates how Flowly's built-in `IHealthCheck` for RabbitMQ surfaces the broker connection state via the ASP.NET Core health endpoint. Running in Docker, the `Receiver` container's `/health` endpoint reports `Healthy` when the RabbitMQ connection is open and `Unhealthy` when it is not — giving Kubernetes a reliable liveness probe.

## Projects

| Project | Purpose |
|---|---|
| `Receiver` | Connects to RabbitMQ via Flowly and exposes `/health` |

## What it demonstrates

- Passing `enableHealthCheck: true` to `UseRabbitMq()` registers an `IHealthCheck` for that transport — off by default so existing registrations are unaffected
- The health check verifies the AMQP connection is open
- `MapHealthChecks("/health")` exposes the result as an HTTP endpoint suitable for Kubernetes liveness/readiness probes
- The Docker Compose `healthcheck` directive uses this endpoint to determine whether the container is ready

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

## How to run

### 1. Start everything with Docker Compose

Run from this directory:

```bash
docker compose up --build
```

Docker Compose starts RabbitMQ, waits for it to pass its own health check, then builds and starts the `Receiver` container.

### 2. What to observe

Once both containers are running, poll the health endpoint from your host:

```bash
curl http://localhost:8080/health
```

Expected output when healthy:

```
Healthy
```

You can also inspect container health status:

```bash
docker compose ps
```

The `receiver` service shows `healthy` once `/health` returns `200 OK`. A broken or missing RabbitMQ connection causes the endpoint to return `503 Service Unavailable`, which Kubernetes would use to restart the pod.

## Notes

- `CreateTopology = false` because this sample only demonstrates connectivity — no message handlers or queues are needed.
- Pass `enableHealthCheck: true` to `UseRabbitMq()` to opt in. The default is `false` so existing registrations gain no new behaviour.
- For multiple RabbitMQ providers (multi-bus), each call independently controls whether its health check is registered. Named providers get distinct check names (`rabbitmq`, `rabbitmq-{name}`).
