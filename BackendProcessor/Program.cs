using BackendProcessor.JobHandlers;
using MessageContracts;
using SimpleTransit.MessageInfrastructure.Registration;
using SimpleTransit.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddAzureServiceBusClient(connectionName: "EmulatorNamespace");

builder.Services
    .AddRepositories(builder.Configuration.GetConnectionString("SqlServer")!)
    .AddJobHandler<PerformStitchingOperationMessage, PerformStitchingOperationJobHandler>(QueuesNames.PerformStitching)
    .AddBatchMessageHandler<RebuildIndexMessage, RebuildIndexBatchHandler>(QueuesNames.RebuildIndex, 10, TimeSpan.FromSeconds(30))
    .AddJobHandlerStateDatabaseMigrations()
    .AddJobMaintenanceBackgroundJobs()
    .RegisterJobStateQueueProcessor();

builder.Services.AddRecurringJob<RecurringSystemImportHandler>("Import System Data", TimeSpan.FromSeconds(30));
builder.Services.AddRecurringJob<RecurringMoreFrequentImportHandler>("Import Frequent Data", TimeSpan.FromSeconds(10));

var app = builder.Build();

app.Run();
