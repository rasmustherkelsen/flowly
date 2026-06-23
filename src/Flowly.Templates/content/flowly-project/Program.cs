using Flowly;
#if (UseInline)
#if (UseRabbitMQ)
using Flowly.RabbitMQ;
#endif
#if (UseAzureServiceBus)
using Flowly.AzureServiceBus;
#endif
#if (UseInMemory)
using Flowly.InMemory;
#endif
#endif
#if (UseOpenTelemetry)
using Flowly.OpenTelemetry;
#endif
#if(!UseInline)
using FlowlyApp;
#endif

#if (NoHttp)
var builder = Host.CreateApplicationBuilder(args);
#else
var builder = WebApplication.CreateBuilder(args);
#endif

#if (UseInline)
#if (UseOpenTelemetry)
builder.AddFlowly(options => options.EnableTelemetry = true, flowlyBuilder =>
#else
builder.AddFlowly(flowlyBuilder =>
#endif
{
#if (UseRabbitMQ)
    flowlyBuilder.UseRabbitMq(connection: "RabbitMQ");
#endif
#if (UseAzureServiceBus)
    flowlyBuilder.UseAzureServiceBus(connection: "AzureServiceBus");
#endif
#if (UseInMemory)
    flowlyBuilder.UseInMemory();
#endif
#if (UseJobTracking && UseSqlServer)
    flowlyBuilder.AddSqlServerJobStateTracking("FlowlyJobs", enableMigrations: true);
#endif
#if (UseJobTracking && UsePostgres)
    flowlyBuilder.AddPostgresJobStateTracking("FlowlyJobs", enableMigrations: true);
#endif
#if (UseJobTracking && UseSQLite)
    flowlyBuilder.AddSQLiteJobStateTracking("FlowlyJobs", enableMigrations: true);
#endif
#if (UseDeadLetterTracking && UseSqlServer)
    flowlyBuilder.AddSqlServerDeadLetterTracking(
        "FlowlyDeadLetters",
        enableMigrations: true);
#endif
#if (UseDeadLetterTracking && UsePostgres)
    flowlyBuilder.AddPostgresDeadLetterTracking(
        "FlowlyDeadLetters",
        enableMigrations: true);
#endif
#if (UseDeadLetterTracking && UseSQLite)
    flowlyBuilder.AddSQLiteDeadLetterTracking(
        "FlowlyDeadLetters",
        enableMigrations: true);
#endif
});
#else
#if (UseOpenTelemetry)
builder.AddFlowly<FlowlyConfiguration>(options => options.EnableTelemetry = true);
#else
builder.AddFlowly<FlowlyConfiguration>();
#endif
#endif
#if (UseOpenTelemetry)

builder.AddFlowlyOpenTelemetry();
#endif
#if (NoHttp)

var host = builder.Build();
host.Run();
#else

var app = builder.Build();
app.Run();
#endif
