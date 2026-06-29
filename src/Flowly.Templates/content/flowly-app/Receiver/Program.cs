using Flowly;
using Receiver;
#if (UseRabbitMQ)
using Flowly.MessageInfrastructure;
#endif
#if (UseOpenTelemetry)
using Flowly.OpenTelemetry;
#endif
#if (UseOtelExportDefault || UseOtelExportJaeger)
using OpenTelemetry;
#endif

var builder = Host.CreateApplicationBuilder(args);

#if (UseRabbitMQ && UseOpenTelemetry)
builder.AddFlowly<FlowlyConfiguration>(options => { options.CreateTopology = true; options.WithTopologyNameResolver<DotCaseTopologyNameResolver>(); options.EnableTelemetry = true; });
#elif (UseRabbitMQ)
builder.AddFlowly<FlowlyConfiguration>(options => { options.CreateTopology = true; options.WithTopologyNameResolver<DotCaseTopologyNameResolver>(); });
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

var host = builder.Build();

host.Run();
