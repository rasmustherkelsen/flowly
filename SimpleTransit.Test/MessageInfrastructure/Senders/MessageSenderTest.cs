using SimpleTransit.MessageInfrastructure.Senders;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SimpleTransit.AzureServiceBusWrappers;
using SimpleTransit.MessageInfrastructure.Model;
using Azure.Messaging.ServiceBus;
using SimpleTransit.Test.Utils;
using SimpleTransit.MessageInfrastructure.Messages;

namespace SimpleTransit.Test.MessageInfrastructure.Senders;

public class MessageSenderTest
{
    public class Send
    {
        [Theory, AutoDataWithCustomization(typeof(SetupMessageSenderForTest))]
        internal async Task MustSubmitMessageToMessageSubmitter(MessageSender messageSender, IMessageSubmitter<MyMessage> messageSubmitter)
        {
            await messageSender.Send(new MyMessage());

            await messageSubmitter.Received(1).Submit(Arg.Is<MyMessage>(x => x != null), Arg.Any<CancellationToken>());
        }
    }

    public class SendJob
    {
        [Theory, AutoDataWithCustomization(typeof(SetupMessageSenderForTest))]
        internal async Task MustSubmitJobToJobSubmitter(MessageSender messageSender, MyJobMessage jobMessage, IJobSubmitter<MyJobMessage> jobSubmitter)
        {
            await messageSender.SendJob(jobMessage);
            await jobSubmitter.Received(1).SubmitJob(Arg.Is<MyJobMessage>(x => x == jobMessage), Arg.Any<CancellationToken>());
        }
    }

    public class SendMessage
    {
        [Theory, AutoDataWithCustomization(typeof(SetupMessageSenderForTest))]
        internal async Task MustSendMessageToServiceBusClient(
            MessageSender messageSender, 
            string queueName,
            Guid messageId,
            string sessionId,
            IServiceBusSender serviceBusSender)
        {
            await messageSender.SendMessage(queueName, messageId, sessionId);

            await serviceBusSender.Received(1).SendMessageAsync(Arg.Is<ServiceBusMessage>(msg =>
                msg.MessageId == messageId.ToString() &&
                msg.SessionId == sessionId));
        }
    }

    public class StartRecurringJob
    {
        [Theory, AutoDataWithCustomization(typeof(SetupMessageSenderForTest))]
        internal async Task MustSendMessageToServiceBusClient(
            MessageSender messageSender,
            Guid jobId,
            IMessageSubmitter<StartRecurringJobMessage> startRecurringJobMessageSubmitter)
        {
            await messageSender.StartRecurringJob(jobId);

            await startRecurringJobMessageSubmitter.Received(1).Submit(Arg.Is<StartRecurringJobMessage>(s => s.JobId == jobId));
        }
    }

    internal record MyMessage;

    internal record MyJobMessage(string Description, string JobTypeName) : IJobMessage;

    private class SetupMessageSenderForTest : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize(new AutoNSubstituteCustomization());

            var messageSubmitter = fixture.Create<IMessageSubmitter<MyMessage>>();
            fixture.Inject(messageSubmitter);

            var jobSubmitter = fixture.Create<IJobSubmitter<MyJobMessage>>();
            fixture.Inject(jobSubmitter);

            var serviceBusClient = fixture.Create<IServiceBusClient>();
            fixture.Inject(serviceBusClient);

            var serviceBusSender = fixture.Create<IServiceBusSender>();
            fixture.Inject(serviceBusSender);

            serviceBusClient.GetServiceBusSender(Arg.Any<string>()).Returns(serviceBusSender);

            var startRecurringJobMessageSubmitter = fixture.Create<IMessageSubmitter<StartRecurringJobMessage>>();
            fixture.Inject(startRecurringJobMessageSubmitter);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(messageSubmitter);
            serviceCollection.AddSingleton(jobSubmitter);
            serviceCollection.AddSingleton(startRecurringJobMessageSubmitter);
            fixture.Inject<IServiceProvider>(serviceCollection.BuildServiceProvider());

            
        }
    }
}