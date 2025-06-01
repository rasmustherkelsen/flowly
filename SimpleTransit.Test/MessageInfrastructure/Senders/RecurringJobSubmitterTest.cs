using AutoFixture;
using AutoFixture.AutoNSubstitute;
using NSubstitute;
using SimpleTransit.MessageInfrastructure;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Senders;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.Senders;

public class RecurringJobSubmitterTest
{
    public class Submit
    {
        [Theory, AutoDataWithCustomization(typeof(SetupRecurringJobSubmitterForTest))]
        internal async Task MustSendMessageToRecurringJobQueue(RecurringJobInvoker recurringJobInvoker, RecurringJob recurringJob, IMessageSender messageSender)
        {
            await recurringJobInvoker.Submit(recurringJob);

            await messageSender.Received(1).SendMessage(QueuesNames.RecurringJobs, recurringJob.JobId, recurringJob.JobTypeName);
        }
    }

    private class SetupRecurringJobSubmitterForTest : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize(new AutoNSubstituteCustomization());
            fixture.Inject(fixture.Create<IMessageSender>());
        }
    }
}