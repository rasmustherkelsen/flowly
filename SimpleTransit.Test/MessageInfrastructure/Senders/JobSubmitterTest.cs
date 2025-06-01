using SimpleTransit.MessageInfrastructure.Senders;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using NSubstitute;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.AzureServiceBusWrappers;
using SimpleTransit.MessageInfrastructure.Messages;
using Azure.Messaging.ServiceBus;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.Senders;

public class JobSubmitterTest
{
    public class SubmitJob
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobSubmitterForTest))]
        internal async Task MustSendMessage(JobSubmitter<MyJobMessage> jobSubmitter, MyJobMessage myJobMessage, IServiceBusSender serviceBusSender)
        {
            await jobSubmitter.SubmitJob(myJobMessage, CancellationToken.None);

            await serviceBusSender.Received(1).SendMessageAsync(Arg.Is<ServiceBusMessage>(msg => msg != null));
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobSubmitterForTest))]
        internal async Task MustSubmitCreateJobStateWhenSubmittingAJob(JobSubmitter<MyJobMessage> jobSubmitter, IMessageSender messageSender, MyJobMessage myJobMessage)
        {
            await jobSubmitter.SubmitJob(myJobMessage);

            await messageSender.Received(1).Send(Arg.Is<CreateJobState>(msg =>
                msg.JobTypeName == myJobMessage.JobTypeName &&
                msg.Description == myJobMessage.Description));
        }
    }

    internal record MyJobMessage(string Description, string JobTypeName) : IJobMessage;

    private class SetupJobSubmitterForTest : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize(new AutoNSubstituteCustomization());

            var serviceBusSender = fixture.Create<IServiceBusSender>();
            fixture.Inject(serviceBusSender);

            var serviceBusClient = fixture.Create<IServiceBusClient>();
            serviceBusClient.GetServiceBusSender(Arg.Any<string>()).Returns(serviceBusSender);
            fixture.Inject(serviceBusClient);

            var queueSettings = new JobSubmitter<MyJobMessage>.QueueSettings("The Job Queue");
            fixture.Inject(queueSettings);

            var messageSender = fixture.Create<IMessageSender>();
            fixture.Inject(messageSender);
        }
    }
}