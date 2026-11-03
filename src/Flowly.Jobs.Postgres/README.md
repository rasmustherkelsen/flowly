# Flowly.Jobs.Postgres

PostgreSQL backend for [Flowly](https://rasmustherkelsen.github.io/flowly/) job state tracking. Persists job records via EF Core and runs migrations automatically at startup.

## Setup

```csharp
builder.AddFlowly(configure => configure
    .UseRabbitMq("RabbitMQ")
    .AddPostgresJobStateTracking("Jobs")
    .AddJobHandler<ProcessReportJob, ProcessReportJobHandler>());
```

Migrations run at startup by default. To disable:

```csharp
.AddPostgresJobStateTracking("Jobs", enableMigrations: false)
```

## Documentation

Job tracking concepts, job lifecycle, and recurring jobs: **https://rasmustherkelsen.github.io/flowly/**
