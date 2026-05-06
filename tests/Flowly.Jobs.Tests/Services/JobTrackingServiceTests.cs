using Flowly.Jobs.Model;
using Flowly.Jobs.Services;
using Flowly.Jobs.Tests.Fakes;

namespace Flowly.Jobs.Tests.Services;

public class JobTrackingServiceTests
{
    public class GetRecurringJobs
    {
        [Fact]
        public async Task ReturnsEmptyCollectionWhenRepositoryReturnsNone()
        {
            var jobStateRepository = new FakeJobStateRepository { RecurringJobsToReturn = [] };
            var jobTrackingService = new JobTrackingService(jobStateRepository);

            var result = await jobTrackingService.GetRecurringJobs();

            Assert.Empty(result);
        }

        [Fact]
        public async Task ProjectsRecurringJobFieldsIntoRecurringJobInfo()
        {
            var jobId = Guid.NewGuid();
            var created = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var started = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero);
            var completed = new DateTimeOffset(2025, 1, 3, 0, 0, 0, TimeSpan.Zero);
            var jobStateRepository = new FakeJobStateRepository
            {
                RecurringJobsToReturn =
                [
                    new RecurringJob(jobId, "JobTypeName", "description", "* * * * *", created, started, completed)
                ]
            };
            var jobTrackingService = new JobTrackingService(jobStateRepository);

            var result = await jobTrackingService.GetRecurringJobs();

            var info = Assert.Single(result);
            Assert.Equal(jobId, info.JobId);
            Assert.Equal("JobTypeName", info.JobTypeName);
            Assert.Equal("description", info.Description);
            Assert.Equal("* * * * *", info.CronExpression);
            Assert.Equal(created, info.Created);
            Assert.Equal(started, info.LastStarted);
            Assert.Equal(completed, info.LastCompleted);
        }
    }

    public class GetJobs
    {
        [Fact]
        public async Task ReturnsRepositoryQueryResult()
        {
            var infos = new List<JobInfo>
            {
                new(Guid.NewGuid(), "Job", "desc", JobState.Started, DateTimeOffset.UtcNow, null, null, null, 0)
            };
            var jobStateRepository = new FakeJobStateRepository { JobInfosToReturn = infos };
            var jobTrackingService = new JobTrackingService(jobStateRepository);

            var result = await jobTrackingService.GetJobs();

            Assert.Same(infos, result);
        }

        [Fact]
        public async Task CallsRepositoryQueryWithDefaultQuery()
        {
            var jobStateRepository = new FakeJobStateRepository();
            var jobTrackingService = new JobTrackingService(jobStateRepository);

            await jobTrackingService.GetJobs();

            var query = Assert.Single(jobStateRepository.QueriesExecuted);
            Assert.Null(query.JobId);
            Assert.Null(query.JobType);
        }
    }
}
