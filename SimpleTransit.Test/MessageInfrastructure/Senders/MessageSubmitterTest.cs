using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Azure.Messaging.ServiceBus;
using NSubstitute;
using SimpleTransit.AzureServiceBusWrappers;
using SimpleTransit.MessageInfrastructure.Senders;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.Senders;

public class MessageSubmitterTest
{
    public class Submit
    {
        [Theory, AutoDataWithCustomization(typeof(SetupMessageSubmitterForTest))]
        internal async Task MustSendMessage(MessageSubmitter<MyMessage> messageSubmitter, IServiceBusSender serviceBusSender)
        {
            await messageSubmitter.Submit(new MyMessage(), CancellationToken.None);

            await serviceBusSender.Received(1).SendMessageAsync(Arg.Is<ServiceBusMessage>(msg => msg != null));
        }
    }

    internal record MyMessage;
    
    private class SetupMessageSubmitterForTest : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize(new AutoNSubstituteCustomization());

            var serviceBusClient = fixture.Create<IServiceBusClient>();
            fixture.Inject(serviceBusClient);

            var serviceBusSender = fixture.Create<IServiceBusSender>();
            serviceBusClient.GetServiceBusSender(Arg.Any<string>()).Returns(serviceBusSender);
            fixture.Inject(serviceBusSender);

            var queueSettings = fixture.Create<MessageSubmitter<MyMessage>.QueueSettings>();
            fixture.Inject(queueSettings);
        }
    }
}