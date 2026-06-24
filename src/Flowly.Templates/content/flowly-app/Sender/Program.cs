using Flowly;
using Sender;
using Sender.Services;
#if (UseOpenTelemetry)
using Flowly.OpenTelemetry;
#endif
#if (UseOtelExportDefault || UseOtelExportJaeger)
using OpenTelemetry;
#endif

var builder = WebApplication.CreateBuilder(args);

#if (UseRabbitMQ && UseOpenTelemetry)
builder.AddFlowly<FlowlyConfiguration>(options => { options.CreateTopology = true; options.EnableTelemetry = true; });
#elif (UseRabbitMQ)
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = true);
#elif (UseOpenTelemetry)
builder.AddFlowly<FlowlyConfiguration>(options => { options.CreateTopology = false; options.EnableTelemetry = true; });
#else
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
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

builder.Services.AddHostedService<MessageSenderService>();
#if (UseJobTracking)
builder.Services.AddHostedService<JobSubmitterService>();
#endif
#if (UseDeadLetterTracking)
builder.Services.AddHostedService<FailingMessageSenderService>();
#endif

var app = builder.Build();

app.Run();
