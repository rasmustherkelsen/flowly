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
    .AddBatchMessageHandler<RebuildIndexMessage, RebuildIndexBatchHandler>(QueuesNames.RebuildIndex, 100, TimeSpan.FromSeconds(30))
    .AddRecurringJob<RecurringSystemImportHandler>("Import System Data", "*/30 * * * * *") // every 30 seconds
    .AddRecurringJob<RecurringMoreFrequentImportHandler>("Import Frequent Data", "*/10 * * * * *"); // every 10 seconds

var app = builder.Build();

app.Run();
