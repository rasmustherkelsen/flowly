using AutoFixture;
using AutoFixture.AutoNSubstitute;
using NSubstitute;
using SimpleTransit.MessageInfrastructure.Maintenance;
using SimpleTransit.Repositories;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.Maintenance;

public class RemoveOldJobsRecurringJobTest
{
    public class Handle
    {
        [Theory, AutoDataWithCustomization(typeof(SetupRemoveOldJobsRecurringJobForTest))]
        internal async Task MustDeleteJobsOlderThanThreeDays(RemoveOldJobsRecurringJob removeOldJobsRecurringJob, IJobStateRepository jobStateRepository)
        {
            await removeOldJobsRecurringJob.Handle(CancellationToken.None);

            await jobStateRepository.Received(1).RemoveJobsOlderThan(TimeSpan.FromDays(3));
        }
    }

    private class SetupRemoveOldJobsRecurringJobForTest : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize(new AutoNSubstituteCustomization());
            fixture.Inject(fixture.Create<IJobStateRepository>());
        }
    }
}