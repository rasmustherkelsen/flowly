# Flowly.Tool

`flowly` CLI tool for [Flowly](https://rasmustherkelsen.github.io/flowly/) queue discovery and infrastructure code generation. Works with both Azure Service Bus and RabbitMQ projects.

## Commands

### Generate Docker Compose

Detects transports and database providers from your project references and generates `docker-compose.yml` with all required local infrastructure:

```bash
flowly docker-compose --project ./Sender --project ./Receiver --output docker-compose.yml
docker compose up -d
```

### Azure Service Bus commands

```bash
# List all queues a project registers
flowly azure-service-bus queues --project ./MyService

# Generate emulator config JSON
flowly azure-service-bus emulator-config \
  --project ./MyService \
  --namespace EmulatorNamespace \
  --output ./servicebus-config.json

# Generate Bicep IaC
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

Pass `--project` multiple times to aggregate queues from several services into a single output.

### Shell Completion

```bash
flowly install-completion --shell zsh      # or bash / powershell
flowly remove-completion --shell zsh
```

## Documentation

**https://rasmustherkelsen.github.io/flowly/**
