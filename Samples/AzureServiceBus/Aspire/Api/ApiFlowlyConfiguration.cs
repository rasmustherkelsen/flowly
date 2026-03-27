using Flowly.AzureServiceBus;
using Flowly.DeadLetters.SqlServer.Registration;
using Flowly.Jobs.Registration;
using Flowly.MessageInfrastructure.Registration;
using MessageContracts;
using Microsoft.EntityFrameworkCore;

namespace Api;

public class ApiFlowlyConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("EmulatorNamespace")
            .AddSqlServerDeadLetterTracking(
                builder.Configuration.GetConnectionString("FlowlyDeadLetters")!,
                enableMigrations: false)
            .AddRepositories(options => options.UseSqlServer(
                builder.Configuration.GetConnectionString("FlowlyJobs")!,
                sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null)))
            .AddJobSubmitter<ProcessOrder>()
            .AddMessageSubmitter<RebuildIndexMessage>()
            .AddMessageSubmitter<SomeQueryMessage>();
    }
}