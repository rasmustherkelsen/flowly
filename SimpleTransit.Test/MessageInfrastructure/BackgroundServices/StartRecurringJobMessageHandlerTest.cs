using AutoFixture;
using AutoFixture.AutoNSubstitute;
using NSubstitute;
using SimpleTransit.MessageInfrastructure;
using SimpleTransit.MessageInfrastructure.MessageHandler;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Senders;
using SimpleTransit.Repositories;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.BackgroundServices;

public class StartRecurringJobMessageHandlerTest
{
    public class Handle
    {
        [Theory, AutoDataWithCustomization(typeof(SetupStartRecurringJobMessageHandlerForTest))]
        internal async Task MustSubmitStartRecurringJobOnRecurringJobsQueue(
            StartRecurringJobMessageHandler startRecurringJobMessageHandler, 
            IMessageContext<StartRecurringJobMessage> messageContext, 
            IMessageSender messageSender,
            RecurringJob recurringJob)
        {
            await startRecurringJobMessageHandler.Handle(messageContext);

            await messageSender.Received(1).SendMessage(QueuesNames.RecurringJobs, recurringJob.JobId, recurringJob.JobTypeName);
        }
        
        [Theory, AutoDataWithCustomization(typeof(SetupStartRecurringJobMessageHandlerForTest))]
        internal async Task MustNotSubmitStartRecurringJobOnRecurringJobsQueueWhenJobIdIsUnknown(
            StartRecurringJobMessageHandler startRecurringJobMessageHandler, 
            IMessageContext<StartRecurringJobMessage> messageContext, 
            IMessageSender messageSender)
        {
            messageContext.Message.Returns(new StartRecurringJobMessage(Guid.NewGuid()));

            await startRecurringJobMessageHandler.Handle(messageContext);

            await messageSender.DidNotReceive().SendMessage(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
        }
    }

    private class SetupStartRecurringJobMessageHandlerForTest : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize(new AutoNSubstituteCustomization());

            var recurrringJob = fixture.Create<RecurringJob>();
            fixture.Inject(recurrringJob);
            
            var jobStateRepository = fixture.Create<IJobStateRepository>();
            fixture.Inject(jobStateRepository);

            jobStateRepository.GetRecurringJobs().Returns([recurrringJob]);
            
            var messageSender = fixture.Create<IMessageSender>();
            fixture.Inject(messageSender);
            
            var messageContext = fixture.Create<IMessageContext<StartRecurringJobMessage>>();
            messageContext.Message.Returns(fixture.Build<StartRecurringJobMessage>().With(x => x.JobId, recurrringJob.JobId).Create());
            fixture.Inject(messageContext);
        }
    }
}