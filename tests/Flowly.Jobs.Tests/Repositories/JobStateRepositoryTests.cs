using Flowly.Jobs.DatabaseModel;
using Flowly.Jobs.Model;
using Flowly.Jobs.Repositories;
using Flowly.Jobs.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.Tests.Repositories;

public class JobStateRepositoryTests
{
    public class FlowlysysCreateJobStateMessage
    {
        [Fact]
        public async Task StoresJobWithCorrectProperties()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = new JobId();
            var typeName = UniqueTypeName();
            var created = DateTime.UtcNow;

            await repository.CreateJobState(new Flowly.Jobs.Messages.FlowlysysCreateJobStateMessage(jobId, typeName, "My description", created));

            await using var context = await factory.CreateDbContextAsync();
            var job = await context.Jobs.SingleAsync(j => j.JobIdentifier == jobId.InnerId);
            Assert.Equal(jobId.InnerId, job.JobIdentifier);
            Assert.Equal("My description", job.Description);
            Assert.Equal(typeName, job.JobTypeName);
        }

        [Fact]
        public async Task SetsInitialStateToCreated()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = new JobId();

            await repository.CreateJobState(new Flowly.Jobs.Messages.FlowlysysCreateJobStateMessage(jobId, UniqueTypeName(), "desc", DateTime.UtcNow));

            await using var context = await factory.CreateDbContextAsync();
            var job = await context.Jobs.SingleAsync(j => j.JobIdentifier == jobId.InnerId);
            Assert.Equal(JobState.Created, job.CurrentState);
        }

        [Fact]
        public async Task CreatesJobTypeForNewTypeName()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var typeName = UniqueTypeName();

            await repository.CreateJobState(new Flowly.Jobs.Messages.FlowlysysCreateJobStateMessage(new JobId(), typeName, "desc", DateTime.UtcNow));

            await using var context = await factory.CreateDbContextAsync();
            var jobType = await context.JobTypes.SingleOrDefaultAsync(t => t.Name == typeName);
            Assert.NotNull(jobType);
        }

        [Fact]
        public async Task WhenSameTypeName_ReusesSameJobTypeId()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var typeName = UniqueTypeName();

            await repository.CreateJobState(new Flowly.Jobs.Messages.FlowlysysCreateJobStateMessage(new JobId(), typeName, "desc", DateTime.UtcNow));
            await repository.CreateJobState(new Flowly.Jobs.Messages.FlowlysysCreateJobStateMessage(new JobId(), typeName, "desc", DateTime.UtcNow));

            await using var context = await factory.CreateDbContextAsync();
            var count = await context.JobTypes.CountAsync(t => t.Name == typeName);
            Assert.Equal(1, count);
        }
    }

    public class FlowlysysCreateRecurringJobStateMessage
    {
        [Fact]
        public async Task WhenJobTypeDoesNotExist_CreatesNewRecurringJob()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = new JobId();
            var typeName = UniqueTypeName();

            await repository.CreateRecurringJobState(
                new Flowly.Jobs.Messages.FlowlysysCreateRecurringJobStateMessage(typeName, "Recurring desc", DateTime.UtcNow, "0 2 * * *"),
                jobId);

            await using var context = await factory.CreateDbContextAsync();
            var job = await context.Jobs.SingleAsync(j => j.JobIdentifier == jobId.InnerId);
            Assert.True(job.IsRecurringJob);
            Assert.Equal("0 2 * * *", job.CronExpression);
            Assert.Equal(typeName, job.JobTypeName);
        }

        [Fact]
        public async Task WhenJobTypeAlreadyExists_ResetsStateAndUpdatesCron()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var typeName = UniqueTypeName();

            var originalJobId = new JobId();
            await repository.CreateRecurringJobState(
                new Flowly.Jobs.Messages.FlowlysysCreateRecurringJobStateMessage(typeName, "desc", DateTime.UtcNow, "0 1 * * *"),
                originalJobId);

            await SetJobStarted(factory, originalJobId);

            await repository.CreateRecurringJobState(
                new Flowly.Jobs.Messages.FlowlysysCreateRecurringJobStateMessage(typeName, "desc", DateTime.UtcNow, "0 3 * * *"),
                new JobId());

            await using var context = await factory.CreateDbContextAsync();
            var jobs = await context.Jobs.Where(j => j.JobTypeName == typeName).ToListAsync();
            var job = Assert.Single(jobs);
            Assert.Equal("0 3 * * *", job.CronExpression);
            Assert.Equal(JobState.Created, job.CurrentState);
            Assert.Null(job.Started);
            Assert.Null(job.Completed);
        }
    }

    public class FlowlysysUpdateJobStateMessage
    {
        [Fact]
        public async Task WithStarted_SetsStartedTimestampAndRetryAttempt()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory);

            await repository.UpdateJobState(new Flowly.Jobs.Messages.FlowlysysUpdateJobStateMessage(jobId, JobState.Started, DateTime.UtcNow, RetryAttempt: 2));

            await using var context = await factory.CreateDbContextAsync();
            var job = await context.Jobs.SingleAsync(j => j.JobIdentifier == jobId.InnerId);
            Assert.Equal(JobState.Started, job.CurrentState);
            Assert.Equal(2, job.RetryAttempt);
            Assert.NotNull(job.Started);
        }

        [Fact]
        public async Task WithStarted_ClearsCompletedTimestamp()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory, completed: DateTimeOffset.UtcNow);

            await repository.UpdateJobState(new Flowly.Jobs.Messages.FlowlysysUpdateJobStateMessage(jobId, JobState.Started, DateTime.UtcNow));

            await using var context = await factory.CreateDbContextAsync();
            var job = await context.Jobs.SingleAsync(j => j.JobIdentifier == jobId.InnerId);
            Assert.Null(job.Completed);
        }

        [Fact]
        public async Task WithCompleted_SetsCompletedTimestamp()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory);

            await repository.UpdateJobState(new Flowly.Jobs.Messages.FlowlysysUpdateJobStateMessage(jobId, JobState.Completed, DateTime.UtcNow));

            await using var context = await factory.CreateDbContextAsync();
            var job = await context.Jobs.SingleAsync(j => j.JobIdentifier == jobId.InnerId);
            Assert.Equal(JobState.Completed, job.CurrentState);
            Assert.NotNull(job.Completed);
        }

        [Fact]
        public async Task WithCompleted_ClearsFaultReason()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory, faultReason: "previous error");

            await repository.UpdateJobState(new Flowly.Jobs.Messages.FlowlysysUpdateJobStateMessage(jobId, JobState.Completed, DateTime.UtcNow));

            await using var context = await factory.CreateDbContextAsync();
            var job = await context.Jobs.SingleAsync(j => j.JobIdentifier == jobId.InnerId);
            Assert.Null(job.FaultReason);
        }

        [Fact]
        public async Task WithUnsupportedState_Throws()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repository.UpdateJobState(new Flowly.Jobs.Messages.FlowlysysUpdateJobStateMessage(jobId, JobState.Failed, DateTime.UtcNow)));
        }
    }

    public class UpdateJobFailed
    {
        [Fact]
        public async Task SetsStateToFailed()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory);

            await repository.UpdateJobFailed(new Flowly.Jobs.Messages.FlowlysysJobFailedMessage(jobId, "something broke", DateTime.UtcNow));

            await using var context = await factory.CreateDbContextAsync();
            var job = await context.Jobs.SingleAsync(j => j.JobIdentifier == jobId.InnerId);
            Assert.Equal(JobState.Failed, job.CurrentState);
        }

        [Fact]
        public async Task StoresFaultReason()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory);

            await repository.UpdateJobFailed(new Flowly.Jobs.Messages.FlowlysysJobFailedMessage(jobId, "something broke", DateTime.UtcNow));

            await using var context = await factory.CreateDbContextAsync();
            var job = await context.Jobs.SingleAsync(j => j.JobIdentifier == jobId.InnerId);
            Assert.Equal("something broke", job.FaultReason);
        }

        [Fact]
        public async Task SetsCompletedTimestamp()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory);

            await repository.UpdateJobFailed(new Flowly.Jobs.Messages.FlowlysysJobFailedMessage(jobId, "err", DateTime.UtcNow));

            await using var context = await factory.CreateDbContextAsync();
            var job = await context.Jobs.SingleAsync(j => j.JobIdentifier == jobId.InnerId);
            Assert.NotNull(job.Completed);
        }
    }

    public class RemoveCompletedJobsOlderThan
    {
        [Fact]
        public async Task RemovesCompletedJobOlderThanThreshold()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory, state: JobState.Completed, completed: DateTimeOffset.UtcNow.AddDays(-2));

            await repository.RemoveCompletedJobsOlderThan(TimeSpan.FromDays(1));

            await using var context = await factory.CreateDbContextAsync();
            Assert.False(await context.Jobs.AnyAsync(j => j.JobIdentifier == jobId.InnerId));
        }

        [Fact]
        public async Task KeepsCompletedJobWithinThreshold()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory, state: JobState.Completed, completed: DateTimeOffset.UtcNow.AddMinutes(-5));

            await repository.RemoveCompletedJobsOlderThan(TimeSpan.FromDays(1));

            await using var context = await factory.CreateDbContextAsync();
            Assert.True(await context.Jobs.AnyAsync(j => j.JobIdentifier == jobId.InnerId));
        }

        [Fact]
        public async Task KeepsRecurringCompletedJob()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory, state: JobState.Completed, completed: DateTimeOffset.UtcNow.AddDays(-2), isRecurring: true);

            await repository.RemoveCompletedJobsOlderThan(TimeSpan.FromDays(1));

            await using var context = await factory.CreateDbContextAsync();
            Assert.True(await context.Jobs.AnyAsync(j => j.JobIdentifier == jobId.InnerId));
        }

        [Fact]
        public async Task KeepsFailedJob()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory, state: JobState.Failed, completed: DateTimeOffset.UtcNow.AddDays(-2));

            await repository.RemoveCompletedJobsOlderThan(TimeSpan.FromDays(1));

            await using var context = await factory.CreateDbContextAsync();
            Assert.True(await context.Jobs.AnyAsync(j => j.JobIdentifier == jobId.InnerId));
        }
    }

    public class RemoveFailedJobsOlderThan
    {
        [Fact]
        public async Task RemovesFailedJobOlderThanThreshold()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory, state: JobState.Failed, completed: DateTimeOffset.UtcNow.AddDays(-2));

            await repository.RemoveFailedJobsOlderThan(TimeSpan.FromDays(1));

            await using var context = await factory.CreateDbContextAsync();
            Assert.False(await context.Jobs.AnyAsync(j => j.JobIdentifier == jobId.InnerId));
        }

        [Fact]
        public async Task KeepsFailedJobWithinThreshold()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory, state: JobState.Failed, completed: DateTimeOffset.UtcNow.AddMinutes(-5));

            await repository.RemoveFailedJobsOlderThan(TimeSpan.FromDays(1));

            await using var context = await factory.CreateDbContextAsync();
            Assert.True(await context.Jobs.AnyAsync(j => j.JobIdentifier == jobId.InnerId));
        }

        [Fact]
        public async Task KeepsRecurringFailedJob()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory, state: JobState.Failed, completed: DateTimeOffset.UtcNow.AddDays(-2), isRecurring: true);

            await repository.RemoveFailedJobsOlderThan(TimeSpan.FromDays(1));

            await using var context = await factory.CreateDbContextAsync();
            Assert.True(await context.Jobs.AnyAsync(j => j.JobIdentifier == jobId.InnerId));
        }

        [Fact]
        public async Task KeepsCompletedJob()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory, state: JobState.Completed, completed: DateTimeOffset.UtcNow.AddDays(-2));

            await repository.RemoveFailedJobsOlderThan(TimeSpan.FromDays(1));

            await using var context = await factory.CreateDbContextAsync();
            Assert.True(await context.Jobs.AnyAsync(j => j.JobIdentifier == jobId.InnerId));
        }
    }

    public class Query
    {
        [Fact]
        public async Task WithNoFilter_ReturnsAllNonRecurringJobs()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            await SeedJob(factory);
            await SeedJob(factory);
            await SeedJob(factory, isRecurring: true);

            var results = await repository.Query(new JobQuery());

            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task FilterByJobId_ReturnsOnlyMatchingJob()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var targetJobId = await SeedJob(factory);
            await SeedJob(factory);

            var results = await repository.Query(JobQuery.ById(targetJobId.InnerId));

            var result = Assert.Single(results);
            Assert.Equal(targetJobId.InnerId, result.JobIdentifier);
        }

        [Fact]
        public async Task FilterByJobType_ReturnsOnlyMatchingTypeJobs()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var typeName = UniqueTypeName();
            await SeedJob(factory, typeName: typeName);
            await SeedJob(factory, typeName: typeName);
            await SeedJob(factory);

            var results = await repository.Query(JobQuery.ByJobType(typeName));

            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.Equal(typeName, r.JobTypeName));
        }

        [Fact]
        public async Task ExcludesRecurringJobs()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            await SeedJob(factory, isRecurring: true);

            var results = await repository.Query(new JobQuery());

            Assert.Empty(results);
        }
    }

    public class GetRecurringJobs
    {
        [Fact]
        public async Task ReturnsOnlyRecurringJobs()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            await SeedJob(factory, isRecurring: true);
            await SeedJob(factory);

            var results = await repository.GetRecurringJobs();

            Assert.Single(results);
        }

        [Fact]
        public async Task MapsAllPropertiesCorrectly()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var typeName = UniqueTypeName();
            var jobId = new JobId();
            await repository.CreateRecurringJobState(
                new Flowly.Jobs.Messages.FlowlysysCreateRecurringJobStateMessage(typeName, "Recurring desc", DateTime.UtcNow, "0 6 * * *"),
                jobId);

            var results = await repository.GetRecurringJobs();

            var job = Assert.Single(results);
            Assert.Equal(jobId.InnerId, job.JobId);
            Assert.Equal(typeName, job.JobTypeName);
            Assert.Equal("Recurring desc", job.Description);
            Assert.Equal("0 6 * * *", job.CronExpression);
        }
    }

    public class FailUncompletedJobsOlderThan
    {
        [Fact]
        public async Task FailsStartedJobOlderThanThreshold()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory, state: JobState.Started, started: DateTimeOffset.UtcNow.AddDays(-2));

            await repository.FailUncompletedJobsOlderThan(TimeSpan.FromDays(1));

            await using var context = await factory.CreateDbContextAsync();
            var job = await context.Jobs.SingleAsync(j => j.JobIdentifier == jobId.InnerId);
            Assert.Equal(JobState.Failed, job.CurrentState);
            Assert.Equal("Hung job", job.FaultReason);
        }

        [Fact]
        public async Task KeepsCompletedJobs()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory, state: JobState.Completed, started: DateTimeOffset.UtcNow.AddDays(-2));

            await repository.FailUncompletedJobsOlderThan(TimeSpan.FromDays(1));

            await using var context = await factory.CreateDbContextAsync();
            var job = await context.Jobs.SingleAsync(j => j.JobIdentifier == jobId.InnerId);
            Assert.Equal(JobState.Completed, job.CurrentState);
        }

        [Fact]
        public async Task KeepsFailedJobs()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory, state: JobState.Failed, started: DateTimeOffset.UtcNow.AddDays(-2));

            await repository.FailUncompletedJobsOlderThan(TimeSpan.FromDays(1));

            await using var context = await factory.CreateDbContextAsync();
            var job = await context.Jobs.SingleAsync(j => j.JobIdentifier == jobId.InnerId);
            Assert.Equal(JobState.Failed, job.CurrentState);
        }

        [Fact]
        public async Task KeepsRecentStartedJob()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory, state: JobState.Started, started: DateTimeOffset.UtcNow.AddMinutes(-5));

            await repository.FailUncompletedJobsOlderThan(TimeSpan.FromDays(1));

            await using var context = await factory.CreateDbContextAsync();
            var job = await context.Jobs.SingleAsync(j => j.JobIdentifier == jobId.InnerId);
            Assert.Equal(JobState.Started, job.CurrentState);
        }

        [Fact]
        public async Task KeepsRecurringJobs()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobStateRepository(factory);
            var jobId = await SeedJob(factory, state: JobState.Started, started: DateTimeOffset.UtcNow.AddDays(-2), isRecurring: true);

            await repository.FailUncompletedJobsOlderThan(TimeSpan.FromDays(1));

            await using var context = await factory.CreateDbContextAsync();
            var job = await context.Jobs.SingleAsync(j => j.JobIdentifier == jobId.InnerId);
            Assert.Equal(JobState.Started, job.CurrentState);
        }
    }

    private static string UniqueTypeName() => $"JobType_{Guid.NewGuid():N}";

    private static async Task<JobId> SeedJob(
        SqliteDbContextFactory factory,
        string? typeName = null,
        JobState state = JobState.Created,
        bool isRecurring = false,
        DateTimeOffset? started = null,
        DateTimeOffset? completed = null,
        string? faultReason = null)
    {
        var jobId = new JobId();
        var resolvedTypeName = typeName ?? UniqueTypeName();

        await using var context = await factory.CreateDbContextAsync();

        var jobType = await context.JobTypes.FirstOrDefaultAsync(t => t.Name == resolvedTypeName);
        if (jobType == null)
        {
            jobType = new JobType { Name = resolvedTypeName };
            context.JobTypes.Add(jobType);
            await context.SaveChangesAsync();
        }

        var job = new Job
        {
            JobIdentifier = jobId.InnerId,
            JobTypeId = jobType.Id,
            JobTypeName = resolvedTypeName,
            Description = "Seeded job",
            Created = DateTimeOffset.UtcNow,
            CurrentState = state,
            IsRecurringJob = isRecurring,
            Started = started,
            Completed = completed,
            FaultReason = faultReason
        };

        context.Jobs.Add(job);
        await context.SaveChangesAsync();

        return jobId;
    }

    private static async Task SetJobStarted(SqliteDbContextFactory factory, JobId jobId)
    {
        await using var context = await factory.CreateDbContextAsync();
        await context.Jobs
            .Where(j => j.JobIdentifier == jobId.InnerId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Started, DateTimeOffset.UtcNow)
                .SetProperty(p => p.CurrentState, JobState.Started));
    }
}
