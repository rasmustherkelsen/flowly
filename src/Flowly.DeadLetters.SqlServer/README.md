# Flowly.DeadLetters.SqlServer

SQL Server backend for [Flowly](https://rasmustherkelsen.github.io/flowly/) dead letter tracking. Persists dead-lettered messages via EF Core and runs migrations automatically at startup.

## Setup

```csharp
builder.AddFlowly(configure => configure
    .UseAzureServiceBus("AzureServiceBus")
    .AddSqlServerDeadLetterTracking("DeadLetters")
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>()
    .WithDeadLetterTracking());
```

Migrations run at startup by default. To disable:

```csharp
.AddSqlServerDeadLetterTracking("DeadLetters", enableMigrations: false)
```

## Documentation

Dead letter tracking concepts and requeue behaviour: **https://rasmustherkelsen.github.io/flowly/**
