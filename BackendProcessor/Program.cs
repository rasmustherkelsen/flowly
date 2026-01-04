using BackendProcessor.JobHandlers;
using Flowly.MessageInfrastructure.Registration;
using MessageContracts;
using Flowly.AzureServiceBus;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddFlowly(args, options => options.CreateTopology = false)
    .UseAzureServiceBus("EmulatorNamespace")
    .AddJobStateTracking(builder.Configuration.GetConnectionString("SqlServer")!);
    
builder.Services
    .AddJobHandler<PerformStitchingOperationMessage, PerformStitchingOperationJobHandler>(QueuesNames.PerformStitching)
    .AddBatchMessageHandler<RebuildIndexMessage, RebuildIndexBatchHandler>(QueuesNames.RebuildIndex, 10, TimeSpan.FromSeconds(30))
    .AddRecurringJob<RecurringSystemImportHandler>("Import System Data", TimeSpan.FromSeconds(30))
    .AddRecurringJob<RecurringMoreFrequentImportHandler>("Import Frequent Data", TimeSpan.FromSeconds(10));

var app = builder.Build();

app.Run();
