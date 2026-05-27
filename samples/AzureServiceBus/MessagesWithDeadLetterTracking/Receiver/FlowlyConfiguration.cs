using Flowly;
using Flowly.AzureServiceBus;
using Messages;
using Receiver.MessageHandlers;

namespace Receiver;

internal class ReceiverFlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus()
            .AddSqlServerDeadLetterTracking(
                builder.Configuration.GetConnectionString("FlowlyDeadLetters")!,
                true,
                options =>
                {
                    options.DeleteDeadLetteredMessagesAfter = TimeSpan.FromMinutes(5);
                    options.DeleteRequeuedMessagesAfter = TimeSpan.FromMinutes(1);
                })
            .AddMessageHandler<FlakyMessage, FlakyMessageHandler>()
            .WithDeadLetterTracking();
    }
}
