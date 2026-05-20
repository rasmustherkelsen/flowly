# Flowly.Jobs

Job state tracking and CRON scheduling for [Flowly](https://rasmustherkelsen.github.io/flowly/). Track long-running work through `Created → Started → Completed / Failed` with persistent state in SQL Server or PostgreSQL.

## Quick Start

Also install a database backend: `Flowly.Jobs.SqlServer` or `Flowly.Jobs.Postgres`.



### Define a job message

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
        await ctx.SaveState(new { Step = "Fetching data" });
        var data = await FetchData(ctx.Message.ReportId, ctx.CancellationToken);
        await ctx.SaveState(new { Step = "Generating PDF", Rows = data.Count });
        await GeneratePdf(data, ctx.CancellationToken);
    }
}
```

### Register and submit

```csharp
builder.AddFlowly(configure => configure
    .UseAzureServiceBus("AzureServiceBus")
    .AddSqlServerJobStateTracking(connectionString)
    .AddJobHandler<ProcessReportJob, ProcessReportJobHandler>()
    .AddJobSubmitter<ProcessReportJob>());
```

```csharp
public class ReportController(IJobMessageSender jobSender)
{
    public Task<Guid> StartReport(DateOnly period, CancellationToken ct)
        => jobSender.QueueJob(new ProcessReportJob(Guid.NewGuid(), period), ct);
}
```

## Recurring Jobs

```csharp
[RecurringJob("Nightly Report", "0 2 * * *")]
public class NightlyReportJob : RecurringJobHandler
{
    public override async Task Handle(CancellationToken ct)
        => await GenerateReport(ct);
}
```

```csharp
builder.AddRecurringJob<NightlyReportJob>();
```

The scheduler polls every 5 seconds and guarantees single execution across replicas using session-based queues.

## Documentation

**https://rasmustherkelsen.github.io/flowly/**
