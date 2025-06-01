using AutoFixture;
using AutoFixture.AutoNSubstitute;
using NSubstitute;
using SimpleTransit.MessageInfrastructure.Maintenance;
using SimpleTransit.Repositories;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.Maintenance;

public class FailHungJobsRecurringJobTest
{
    public class Handle
    {
        [Theory, AutoDataWithCustomization(typeof(SetupFailHungJobsRecurringJobForTest))]
        internal async Task MustUseJobStateRepositoryToFailUncompletedJobsOlderThan3Hours(FailHungJobsRecurringJob failHungJobsRecurringJob, IJobStateRepository jobStateRepository)
        {
            await failHungJobsRecurringJob.Handle(CancellationToken.None);

            await jobStateRepository.Received(1).FailUncompletedJobsOlderThan(Arg.Is<TimeSpan>(x => x == TimeSpan.FromHours(3)));
        }
    }

    private class SetupFailHungJobsRecurringJobForTest : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize(new AutoNSubstituteCustomization());
            fixture.Inject(fixture.Create<IJobStateRepository>());
        }
    }
}