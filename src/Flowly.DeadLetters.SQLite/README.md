# Flowly.DeadLetters.SQLite

SQLite backend for [Flowly](https://rasmustherkelsen.github.io/flowly/) dead letter tracking. Persists dead-lettered messages via EF Core and runs migrations automatically at startup.

## Setup

```csharp
builder.AddFlowly(configure => configure
    .UseAzureServiceBus("AzureServiceBus")
    .AddSQLiteDeadLetterTracking(
        builder.Configuration.GetConnectionString("DeadLetters")!)
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>()
    .WithDeadLetterTracking());
```

Migrations run at startup by default. To disable:

```csharp
.AddSQLiteDeadLetterTracking(connectionString, enableMigrations: false)
```

## Documentation

Dead letter tracking concepts and requeue behaviour: **https://rasmustherkelsen.github.io/flowly/**
