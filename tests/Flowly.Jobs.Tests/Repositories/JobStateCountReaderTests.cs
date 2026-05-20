using Flowly.Jobs.DatabaseModel;
using Flowly.Jobs.Repositories;
using Flowly.Jobs.Tests.Helpers;

namespace Flowly.Jobs.Tests.Repositories;

public class JobStateCountReaderTests
{
    public class CountJobsInState
    {
        [Fact]
        public async Task WithNoJobs_ReturnsZero()
        {
            using var factory = new SqliteDbContextFactory();
            var reader = new JobStateCountReader(factory);

            var count = await reader.CountJobsInState(JobState.Created, CancellationToken.None);

            Assert.Equal(0, count);
        }

        [Fact]
        public async Task WithMatchingJobs_ReturnsCorrectCount()
        {
            using var factory = new SqliteDbContextFactory();
            var reader = new JobStateCountReader(factory);
            await SeedJob(factory, JobState.Created);
            await SeedJob(factory, JobState.Created);

            var count = await reader.CountJobsInState(JobState.Created, CancellationToken.None);

            Assert.Equal(2, count);
        }

        [Fact]
        public async Task WithMixedStates_CountsOnlyRequestedState()
        {
            using var factory = new SqliteDbContextFactory();
            var reader = new JobStateCountReader(factory);
            await SeedJob(factory, JobState.Created);
            await SeedJob(factory, JobState.Started);
            await SeedJob(factory, JobState.Completed);
            await SeedJob(factory, JobState.Failed);

            var createdCount = await reader.CountJobsInState(JobState.Created, CancellationToken.None);
            var startedCount = await reader.CountJobsInState(JobState.Started, CancellationToken.None);

            Assert.Equal(1, createdCount);
            Assert.Equal(1, startedCount);
        }
    }

    private static async Task SeedJob(SqliteDbContextFactory factory, JobState state)
    {
        var typeName = $"JobType_{Guid.NewGuid():N}";

        await using var context = await factory.CreateDbContextAsync();

        var jobType = new JobType { Name = typeName };
        context.JobTypes.Add(jobType);
        await context.SaveChangesAsync();

        var job = new Job
        {
            JobIdentifier = Guid.NewGuid(),
            JobTypeId = jobType.Id,
            JobTypeName = typeName,
            Description = "Seeded job",
            Created = DateTimeOffset.UtcNow,
            CurrentState = state
        };

        context.Jobs.Add(job);
        await context.SaveChangesAsync();
    }
}
