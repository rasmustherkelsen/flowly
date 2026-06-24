using Flowly;
using App;
using App.Services;
#if (UseDashboard)
using Flowly.Dashboard;
#endif
#if (UseOpenTelemetry)
using Flowly.OpenTelemetry;
#endif
#if (UseOtelExportDefault || UseOtelExportJaeger)
using OpenTelemetry;
#endif

#if (UseDashboard)
var builder = WebApplication.CreateBuilder(args);
#else
var builder = Host.CreateApplicationBuilder(args);
#endif

#if (UseDashboard)
builder.Services.AddFlowlyDashboard();
#endif
#if (UseOpenTelemetry)
builder.AddFlowly<FlowlyConfiguration>(options => options.EnableTelemetry = true);
#else
builder.AddFlowly<FlowlyConfiguration>();
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

#if (UseDashboard)
var app = builder.Build();

app.UseFlowlyDashboard();

app.Run();
#else
var host = builder.Build();

host.Run();
#endif
