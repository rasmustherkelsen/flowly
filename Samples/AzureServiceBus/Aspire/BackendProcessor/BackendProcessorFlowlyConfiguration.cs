using BackendProcessor.JobHandlers;
using Flowly.AzureServiceBus;
using Flowly.Jobs.Registration;
using Flowly.Jobs.SqlServer.Registration;
using Flowly.MessageInfrastructure.Registration;
using MessageContracts;

namespace BackendProcessor;

public class BackendProcessorFlowlyConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("EmulatorNamespace")
            .AddSqlServerJobStateTracking(builder.Configuration.GetConnectionString("FlowlyJobs")!)
            .AddJobHandler<ProcessOrder, OrderProcessor>(maxConcurrentCalls: 5)
            .AddBatchMessageHandler<RebuildIndexMessage, RebuildIndexBatchHandler>()
            .AddRecurringJob<RecurringImportHandler>()
            .AddRecurringJob<FrequentlyRecurringHandler>()
            .AddMessageHandler<SomeQueryMessage, SomeQueryProcessor>(maxConcurrentCalls: 2);
    }
}