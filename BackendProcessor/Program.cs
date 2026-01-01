using BackendProcessor.JobHandlers;
using MessageContracts;
using SimpleTransit.MessageInfrastructure.Registration;
using SimpleTransit.AzureServiceBus;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddAzureServiceBusClient(connectionName: "EmulatorNamespace");

builder.Services
    .AddSimpleTransit(args)
    .UseAzureServiceBus("EmulatorNamespace")
    .AddJobStateTracking(builder.Configuration.GetConnectionString("SqlServer")!);
    
builder.Services
    .AddJobHandler<PerformStitchingOperationMessage, PerformStitchingOperationJobHandler>(QueuesNames.PerformStitching)
    .AddBatchMessageHandler<RebuildIndexMessage, RebuildIndexBatchHandler>(QueuesNames.RebuildIndex, 10, TimeSpan.FromSeconds(30))
    .AddRecurringJob<RecurringSystemImportHandler>("Import System Data", TimeSpan.FromSeconds(30))
    .AddRecurringJob<RecurringMoreFrequentImportHandler>("Import Frequent Data", TimeSpan.FromSeconds(10));

var app = builder.Build();

app.Run();
