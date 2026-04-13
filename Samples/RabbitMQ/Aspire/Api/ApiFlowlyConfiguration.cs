using Flowly.DeadLetters.Postgres.Registration;
using Flowly.Jobs.Postgres.Registration;
using Flowly.Jobs.Registration;
using Flowly.MessageInfrastructure.Registration;
using Flowly.RabbitMQ;
using MessageContracts;

namespace Api;

public class ApiFlowlyConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseRabbitMq(connection: "RabbitMQ")
            .AddPostgresDeadLetterTracking(builder.Configuration.GetConnectionString("FlowlyDeadLetters")!, enableMigrations: false)
            .AddJobStateTrackingClient(builder.Configuration.GetConnectionString("FlowlyJobs")!)
            .AddJobSubmitter<ProcessOrder>()
            .AddMessageSubmitter<RebuildIndexMessage>()
            .AddMessageSubmitter<SomeQueryMessage>();
    }
}
