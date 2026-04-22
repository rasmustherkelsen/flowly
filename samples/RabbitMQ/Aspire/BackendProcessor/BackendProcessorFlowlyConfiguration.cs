using BackendProcessor.EventHandlers;
using BackendProcessor.JobHandlers;
using Flowly;
using Flowly.OpenTelemetry;
using Flowly.RabbitMQ;
using MessageContracts;

namespace BackendProcessor;

public class BackendProcessorFlowlyConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseRabbitMq(connection: "RabbitMQ")
            .AddPostgresJobStateTracking(
                builder.Configuration.GetConnectionString("FlowlyJobs")!,
                true,
                options =>
                {
                    options.DeleteCompletedJobsAfter = TimeSpan.FromMinutes(3);
                    options.DeleteFailedJobsAfter = TimeSpan.FromMinutes(10);
                })
            .AddPostgresDeadLetterTracking(
                builder.Configuration.GetConnectionString("FlowlyDeadLetters")!,
                true,
                options =>
                {
                    options.DeleteDeadLetteredMessagesAfter = TimeSpan.FromMinutes(5);
                    options.DeleteRequeuedMessagesAfter = TimeSpan.FromMinutes(1);
                })
            .AddJobHandler<ProcessOrder, OrderProcessor>()
            .AddBatchMessageHandler<RebuildIndexMessage, RebuildIndexBatchHandler>()
            .AddRecurringJob<RecurringImportHandler>()
            .AddRecurringJob<FrequentlyRecurringHandler>()
            .AddMessageHandler<SomeQueryMessage, SomeQueryProcessor>()
            .WithDeadLetterTracking()
            .AddEventHandler<OrderProcessedEvent, OrderProcessedEventHandler>()
            .AddEventSubmitter<OrderProcessedEvent>();

        builder.Services.AddOpenTelemetry()
            .WithMetrics(m => m.AddFlowlyInstrumentation())
            .WithTracing(t => t.AddFlowlyInstrumentation());
    }
}