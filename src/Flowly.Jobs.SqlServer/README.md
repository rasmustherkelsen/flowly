# Flowly.Jobs.SqlServer

SQL Server backend for [Flowly](https://rasmustherkelsen.github.io/flowly/) job state tracking. Persists job records via EF Core and runs migrations automatically at startup.

## Setup

```csharp
builder.AddFlowly(configure => configure
    .UseAzureServiceBus("AzureServiceBus")
    .AddSqlServerJobStateTracking(
        builder.Configuration.GetConnectionString("Jobs")!)
    .AddJobHandler<ProcessReportJob, ProcessReportJobHandler>());
```

Migrations run at startup by default. To disable:

```csharp
.AddSqlServerJobStateTracking(connectionString, enableMigrations: false)
```

## Documentation

Job tracking concepts, job lifecycle, and recurring jobs: **https://rasmustherkelsen.github.io/flowly/**
