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
#if (UseRabbitMQ)
using Flowly.MessageInfrastructure;
#endif
#if (UseOpenTelemetry)
using Flowly.OpenTelemetry;
#endif
#if (UseOtelExportDefault || UseOtelExportJaeger)
using OpenTelemetry;
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
#if (UseRabbitMQ && UseOpenTelemetry)
builder.AddFlowly(options => { options.WithTopologyNameResolver<DotCaseTopologyNameResolver>(); options.EnableTelemetry = true; }, flowlyBuilder =>
#elif (UseRabbitMQ)
builder.AddFlowly(options => options.WithTopologyNameResolver<DotCaseTopologyNameResolver>(), flowlyBuilder =>
#elif (UseOpenTelemetry)
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
#if (UseRabbitMQ && UseOpenTelemetry)
builder.AddFlowly<FlowlyConfiguration>(options => { options.WithTopologyNameResolver<DotCaseTopologyNameResolver>(); options.EnableTelemetry = true; });
#elif (UseRabbitMQ)
builder.AddFlowly<FlowlyConfiguration>(options => options.WithTopologyNameResolver<DotCaseTopologyNameResolver>());
#elif (UseOpenTelemetry)
builder.AddFlowly<FlowlyConfiguration>(options => options.EnableTelemetry = true);
#else
builder.AddFlowly<FlowlyConfiguration>();
#endif
#endif
#if (UseOpenTelemetry)

builder.AddFlowlyOpenTelemetry();
#endif
#if (UseOtelExportDefault)

var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
if (useOtlpExporter)
    builder.Services.AddOpenTelemetry().UseOtlpExporter();
#endif
#if (UseOtelExportJaeger)

builder.Services.AddOpenTelemetry().UseOtlpExporter();
#endif
#if (UseOtelExportZipkin)

builder.Services.AddOpenTelemetry().WithTracing(t => t.AddZipkinExporter());
#endif
#if (NoHttp)

var host = builder.Build();
host.Run();
#else

var app = builder.Build();
app.Run();
#endif
