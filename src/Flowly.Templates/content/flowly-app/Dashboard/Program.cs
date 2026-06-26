using Flowly;
using Flowly.Dashboard;
using Dashboard;
#if (UseOpenTelemetry)
using Flowly.OpenTelemetry;
#endif
#if (UseOtelExportDefault || UseOtelExportJaeger)
using OpenTelemetry;
#endif
#if (UseOpenTelemetry)
using OpenTelemetry.Trace;
#endif

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFlowlyDashboard(options => options.PathPrefix = string.Empty);
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
builder.Services.AddOpenTelemetry().WithTracing(t => t.AddAspNetCoreInstrumentation());
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

var app = builder.Build();

app.UseFlowlyDashboard();

app.Run();
