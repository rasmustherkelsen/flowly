using BackendProcessor.JobHandlers;
using Flowly.AzureServiceBus;
using Flowly.Jobs.Registration;
using Flowly.MessageInfrastructure.Registration;
using MessageContracts;

namespace BackendProcessor;

public class BackendProcessorFlowlyConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("EmulatorNamespace")
            .AddJobStateTracking(builder.Configuration.GetConnectionString("FlowlyJobs")!)
            .AddJobHandler<PerformStitchingOperationMessage, PerformStitchingOperationJobHandler>()
            .AddBatchMessageHandler<RebuildIndexMessage, RebuildIndexBatchHandler>(100, TimeSpan.FromSeconds(30))
            .AddRecurringJob<RecurringSystemImportHandler>("Import System Data", "*/30 * * * * *") // every 30 seconds
            .AddRecurringJob<RecurringMoreFrequentImportHandler>("Import Frequent Data", "*/10 * * * * *"); // every 10 seconds       
    }
}