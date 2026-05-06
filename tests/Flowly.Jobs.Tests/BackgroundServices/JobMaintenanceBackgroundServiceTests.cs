using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Flowly.Jobs.Tests.BackgroundServices;

public class JobMaintenanceBackgroundServiceTests
{
    public class PerformMaintenance
    {
        [Fact]
        public async Task AlwaysFailsUncompletedJobsOlderThanHungJobThreshold()
        {
            var repository = new FakeJobStateRepository();
            var service = BuildService(new JobStateTrackingOptions());

            await service.PerformMaintenance(repository);

            var age = Assert.Single(repository.UncompletedFailedOlderThan);
            Assert.Equal(JobMaintenanceBackgroundService.HungJobThreshold, age);
        }

        [Fact]
        public async Task WhenDeleteCompletedJobsAfterIsSet_RemovesCompletedJobsOlderThanSpecifiedAge()
        {
            var repository = new FakeJobStateRepository();
            var service = BuildService(new JobStateTrackingOptions { DeleteCompletedJobsAfter = TimeSpan.FromDays(30) });

            await service.PerformMaintenance(repository);

            var age = Assert.Single(repository.CompletedJobsRemovedOlderThan);
            Assert.Equal(TimeSpan.FromDays(30), age);
        }

        [Fact]
        public async Task WhenDeleteCompletedJobsAfterIsNull_DoesNotRemoveCompletedJobs()
        {
            var repository = new FakeJobStateRepository();
            var service = BuildService(new JobStateTrackingOptions { DeleteCompletedJobsAfter = null });

            await service.PerformMaintenance(repository);

            Assert.Empty(repository.CompletedJobsRemovedOlderThan);
        }

        [Fact]
        public async Task WhenDeleteFailedJobsAfterIsSet_RemovesFailedJobsOlderThanSpecifiedAge()
        {
            var repository = new FakeJobStateRepository();
            var service = BuildService(new JobStateTrackingOptions { DeleteFailedJobsAfter = TimeSpan.FromDays(7) });

            await service.PerformMaintenance(repository);

            var age = Assert.Single(repository.FailedJobsRemovedOlderThan);
            Assert.Equal(TimeSpan.FromDays(7), age);
        }

        [Fact]
        public async Task WhenDeleteFailedJobsAfterIsNull_DoesNotRemoveFailedJobs()
        {
            var repository = new FakeJobStateRepository();
            var service = BuildService(new JobStateTrackingOptions { DeleteFailedJobsAfter = null });

            await service.PerformMaintenance(repository);

            Assert.Empty(repository.FailedJobsRemovedOlderThan);
        }

        [Fact]
        public async Task WithAllOptionsSet_CallsAllThreeMethods()
        {
            var repository = new FakeJobStateRepository();
            var service = BuildService(new JobStateTrackingOptions
            {
                DeleteCompletedJobsAfter = TimeSpan.FromDays(30),
                DeleteFailedJobsAfter = TimeSpan.FromDays(7)
            });

            await service.PerformMaintenance(repository);

            Assert.Single(repository.UncompletedFailedOlderThan);
            Assert.Single(repository.CompletedJobsRemovedOlderThan);
            Assert.Single(repository.FailedJobsRemovedOlderThan);
        }
    }

    private static JobMaintenanceBackgroundService BuildService(JobStateTrackingOptions opts)
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        return new JobMaintenanceBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(opts));
    }
}
