using BackendProcessor.JobHandlers;
using Flowly.AzureServiceBus;
using Flowly.DeadLetters.Registration;
using Flowly.DeadLetters.SqlServer.Registration;
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
            .AddSqlServerDeadLetterTracking(builder.Configuration.GetConnectionString("FlowlyDeadLetters")!)
            .AddJobHandler<ProcessOrder, OrderProcessor>()
            .AddBatchMessageHandler<RebuildIndexMessage, RebuildIndexBatchHandler>()
            .AddRecurringJob<RecurringImportHandler>()
            .AddRecurringJob<FrequentlyRecurringHandler>()
            .AddMessageHandler<SomeQueryMessage, SomeQueryProcessor>()
            .WithDeadLetterTracking();
    }
}