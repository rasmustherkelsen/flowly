using Api.Handlers.EventHandlers;
using Api.Handlers.JobHandlers;
using Api.Messages;
using Flowly;
using Flowly.DeadLetters.SQLite;
using Flowly.InMemory;
using Flowly.Jobs;
using Flowly.Jobs.SQLite;

internal class ApiFlowlyConfiguration : IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseInMemory()
            .AddSQLiteJobStateTracking(
                "Data Source=flowly-jobs;Mode=Memory;Cache=Shared",
                configure: opts =>
                {
                    opts.DeleteCompletedJobsAfter = TimeSpan.FromMinutes(3);
                    opts.DeleteFailedJobsAfter = TimeSpan.FromMinutes(10);
                })
            .AddSQLiteDeadLetterTracking(
                "Data Source=flowly-deadletters;Mode=Memory;Cache=Shared",
                configure: opts =>
                {
                    opts.DeleteDeadLetteredMessagesAfter = TimeSpan.FromMinutes(5);
                    opts.DeleteRequeuedMessagesAfter = TimeSpan.FromMinutes(1);
                })
            .AddJobHandler<ProcessOrder, OrderProcessor>()
            .AddBatchMessageHandler<RebuildIndexMessage, RebuildIndexBatchHandler>()
            .AddRecurringJob<RecurringImportHandler>()
            .AddRecurringJob<FrequentlyRecurringHandler>()
            .AddMessageHandler<SomeQueryMessage, SomeQueryProcessor>()
            .WithDeadLetterTracking()
            .AddEventHandler<OrderProcessedEvent, OrderProcessedEventHandler>()
            .AddEventSubmitter<OrderProcessedEvent>()
            .AddJobSubmitter<ProcessOrder>()
            .AddMessageSubmitter<RebuildIndexMessage>()
            .AddMessageSubmitter<SomeQueryMessage>();
    }
}
