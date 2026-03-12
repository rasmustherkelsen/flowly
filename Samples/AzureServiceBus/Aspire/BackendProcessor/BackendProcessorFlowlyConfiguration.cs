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
            .AddJobHandler<ProcessOrder, OrderProcessor>()
            .AddBatchMessageHandler<RebuildIndexMessage, RebuildIndexBatchHandler>()
            .AddRecurringJob<RecurringSystemImportHandler>()
            .AddRecurringJob<RecurringMoreFrequentImportHandler>()
            .AddMessageHandler<SomeQueryMessage, SomeQueryProcessor>();
    }
}