using System.Text.Json;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimpleTransit.DatabaseModel.JobStateDatabase;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.Repositories;
using SimpleTransit.Test.Utils;
using JobState = SimpleTransit.MessageInfrastructure.Model.JobState;

namespace SimpleTransit.Test.Repositories;

public class JobStateRepositoryTest
{
    public class CreateJobState
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobStateRepositoryForTest))]
        internal async Task MustAddJobToDatabase(JobStateRepository jobStateRepository, JobStateDataContext dbContext, SimpleTransit.MessageInfrastructure.Messages.CreateJobState createJobState)
        {
            await jobStateRepository.CreateJobState(createJobState);

            var job = await dbContext.Jobs.SingleAsync(x => x.JobId == createJobState.JobId);

            Assert.Equal(createJobState.JobId, job.JobId);
            Assert.Equal(createJobState.Description, job.Description);
            Assert.Equal(createJobState.TimeStamp, job.Created);
        }
    }

    public class CreateRecurringJobState
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobStateRepositoryForTest))]
        internal async Task MustAddNewRecurringJobToDatabase(
            JobStateRepository jobStateRepository,
            JobStateDataContext dbContext,
            SimpleTransit.MessageInfrastructure.Messages.CreateRecurringJobState createRecurringJobStateMessage)
        {
            await jobStateRepository.CreateRecurringJobState(createRecurringJobStateMessage);

            Assert.Collection(dbContext.Jobs.Include(x => x.JobType), job =>
            {
                Assert.True(job.IsRecurringJob);
                Assert.Equal(createRecurringJobStateMessage.JobTypeName, job.JobType!.Name);
                Assert.Equal(createRecurringJobStateMessage.Interval, job.Interval);
                Assert.Equal(JobState.Created, job.CurrentState);
                Assert.Null(job.Started);
                Assert.Null(job.Completed);
                Assert.Null(job.CustomJobState);
            });
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobStateRepositoryForTest))]
        internal async Task MustUpdateExistingRecurringJobInDatabase(
            JobStateRepository jobStateRepository,
            JobStateDataContext dbContext,
            SimpleTransit.MessageInfrastructure.Messages.CreateRecurringJobState createRecurringJobStateMessage,
            IFixture fixture)
        {
            await jobStateRepository.CreateRecurringJobState(createRecurringJobStateMessage);

            var existingRecurringJobState = fixture.Build<SimpleTransit.MessageInfrastructure.Messages.CreateRecurringJobState>()
                .With(x => x.JobTypeName, createRecurringJobStateMessage.JobTypeName)
                .Create();

            await jobStateRepository.CreateRecurringJobState(existingRecurringJobState);

            Assert.Collection(dbContext.Jobs.Include(x => x.JobType), job =>
            {
                Assert.True(job.IsRecurringJob);
                Assert.Equal(createRecurringJobStateMessage.JobTypeName, job.JobType!.Name);
                Assert.Equal(existingRecurringJobState.Interval, job.Interval);
                Assert.Equal(JobState.Created, job.CurrentState);
                Assert.Null(job.Started);
                Assert.Null(job.Completed);
                Assert.Null(job.CustomJobState);
            });
        }
    }

    public class UpdateJobState
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobStateRepositoryForTest), typeof(StartedJobState))]
        internal async Task MustUpdateStartedJob(
            JobStateRepository jobStateRepository,
            SimpleTransit.MessageInfrastructure.Messages.UpdateJobState updateJobState,
            JobStateDataContext dbContext)
        {
            await jobStateRepository.UpdateJobState(updateJobState);

            Assert.Collection(dbContext.Jobs, job =>
            {
                Assert.Equal(updateJobState.TimeStamp, job.Started);
                Assert.Equal(JobState.Started, job.CurrentState);
            });
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobStateRepositoryForTest), typeof(CompletedJobState))]
        internal async Task MustUpdateJobCompleted(
            JobStateRepository jobStateRepository,
            SimpleTransit.MessageInfrastructure.Messages.UpdateJobState updateJobState,
            JobStateDataContext dbContext)
        {
            await jobStateRepository.UpdateJobState(updateJobState);

            Assert.Collection(dbContext.Jobs, job =>
            {
                Assert.Equal(updateJobState.TimeStamp, job.Completed);
                Assert.Equal(JobState.Completed, job.CurrentState);
            });
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobStateRepositoryForTest), typeof(FailedJobState))]
        internal async Task MustThrowIfStateIsUnsupported(JobStateRepository jobStateRepository, SimpleTransit.MessageInfrastructure.Messages.UpdateJobState updateJobState)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => jobStateRepository.UpdateJobState(updateJobState));

            Assert.StartsWith("Unsupported JobState", ex.Message);
        }
    }

    public class UpdateJobFailed
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobStateRepositoryForTest), typeof(HasJobToFail))]
        internal async Task MustUpdatedWithFailedJobInformation(
            JobStateRepository jobStateRepository,
            SimpleTransit.MessageInfrastructure.Messages.JobFailed jobFailed,
            JobStateDataContext dbContext)
        {
            await jobStateRepository.UpdateJobFailed(jobFailed);

            Assert.Collection(dbContext.Jobs, job =>
            {
                Assert.Equal(JobState.Failed, job.CurrentState);
                Assert.Equal(jobFailed.FaultReason, job.FaultReason);
                Assert.Equal(jobFailed.TimeStamp, job.Completed);
            });
        }
    }

    public class UpdateJobCustomState
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobStateRepositoryForTest), typeof(WithCustomJobStateUpdate))]
        internal async Task MustUpdateCustomJobState(
            JobStateRepository jobStateRepository,
            SimpleTransit.MessageInfrastructure.Messages.UpdateCustomJobState updateCustomJobStateMessage,
            JobStateDataContext dbContext)
        {
            await jobStateRepository.UpdateJobCustomState(updateCustomJobStateMessage);

            Assert.Collection(dbContext.CustomJobStates, jobState =>
            {
                Assert.Equal(JsonSerializer.Serialize(updateCustomJobStateMessage.CustomState), jobState.CustomState);
                Assert.Equal(updateCustomJobStateMessage.JobId, jobState.JobIdentifier);
            });
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobStateRepositoryForTest), typeof(WithCustomJobStateUpdate))]
        internal async Task MustUpdateExistingCustomJobState(
            JobStateRepository jobStateRepository,
            SimpleTransit.MessageInfrastructure.Messages.UpdateCustomJobState updateCustomJobStateMessage,
            JobStateDataContext dbContext)
        {
            await jobStateRepository.UpdateJobCustomState(updateCustomJobStateMessage);

            updateCustomJobStateMessage = updateCustomJobStateMessage with { CustomState = DateTime.UtcNow };
            await jobStateRepository.UpdateJobCustomState(updateCustomJobStateMessage);

            Assert.Collection(dbContext.CustomJobStates, jobState =>
            {
                Assert.Equal(JsonSerializer.Serialize(updateCustomJobStateMessage.CustomState), jobState.CustomState);
                Assert.Equal(updateCustomJobStateMessage.JobId, jobState.JobIdentifier);
            });
        }
    }

    public class GetRecurringJobs
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobStateRepositoryForTest), typeof(WithRecurringJobs))]
        internal async Task MustReturnOnlyRecurringJobs(JobStateRepository jobStateRepository, Job expectedRecurringJob)
        {
            var jobs = await jobStateRepository.GetRecurringJobs();

            Assert.Collection(jobs, job =>
            {
                Assert.Equal(expectedRecurringJob.JobId, job.JobId);
                Assert.Equal(expectedRecurringJob.JobType!.Name, job.JobTypeName);
                Assert.Equal(expectedRecurringJob.Created, job.Created);
                Assert.Equal(expectedRecurringJob.Interval, job.Interval);
            });
        }
    }

    public class FailUncompletedJobsOlderThan
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobStateRepositoryForTest), typeof(WithHungJobs))]
        internal async Task MustSetOldUncompletedJobsToHungAndFailed(JobStateRepository jobStateRepository, Job notHungJob, JobStateDataContext dbContext)
        {
            await jobStateRepository.FailUncompletedJobsOlderThan(TimeSpan.FromHours(3));

            Assert.Collection(dbContext.Jobs,
                job =>
                {
                    Assert.Equal(JobState.Created, job.CurrentState);
                    Assert.Equal(notHungJob.JobId, job.JobId);
                },
                job => Assert.Equal(JobState.Failed, job.CurrentState));
        }
    }

    public class RemoveJobsOlderThan
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobStateRepositoryForTest), typeof(WithOldJobs))]
        internal async Task MustReturnOnlyRecurringJobs(
            JobStateRepository jobStateRepository,
            Job expectedRemainingJob,
            JobStateDataContext dbContext)
        {
            await jobStateRepository.RemoveJobsOlderThan(TimeSpan.FromHours(1));

            Assert.Collection(dbContext.Jobs, job => { Assert.Equal(expectedRemainingJob.JobId, job.JobId); });
        }
    }

    private class SetupJobStateRepositoryForTest : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize(new AutoNSubstituteCustomization());

            string connectionString = $"DataSource={Guid.NewGuid()};mode=memory;cache=shared";

            var connection = new SqliteConnection(connectionString);
            connection.Open();

            using (var dbContext = new JobStateDataContext(new DbContextOptionsBuilder<JobStateDataContext>().UseSqlite(connection).Options))
            {
                dbContext.Database.EnsureCreated();
            }

            fixture.Register(() =>
            {
                var dbContext = new JobStateDataContext(new DbContextOptionsBuilder<JobStateDataContext>().UseSqlite(connection).Options);
                return dbContext;
            });

            fixture.Register((IFixture f) =>
            {
                var databaseConnectionFactory = Substitute.For<IDbContextFactory<JobStateDataContext>>();
                databaseConnectionFactory.CreateDbContextAsync().Returns(_ => f.Create<JobStateDataContext>());
                return databaseConnectionFactory;
            });
        }
    }

    private class HasExistingJob : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            var jobId = Guid.NewGuid();

            var dbContext = fixture.Create<JobStateDataContext>();

            var jobType = new JobType { Name = "The Job Type" };
            var job = new Job
            {
                JobId = jobId, 
                Description = "Some Description", 
                Created = DateTime.UtcNow, 
                JobType = jobType,
                CustomJobState = new CustomJobState
                {
                    JobIdentifier = jobId
                }
            };

            dbContext.Jobs.Add(job);
            dbContext.JobTypes.Add(jobType);

            dbContext.SaveChanges();

            fixture.Inject(job);
        }
    }

    private class StartedJobState() : JobStateBase(JobState.Started);

    private class CompletedJobState() : JobStateBase(JobState.Completed);

    private class FailedJobState() : JobStateBase(JobState.Failed);

    private abstract class JobStateBase(JobState jobState) : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize(new HasExistingJob());
            fixture.Inject(new SimpleTransit.MessageInfrastructure.Messages.UpdateJobState(fixture.Create<Job>().JobId, jobState, DateTime.UtcNow));
        }
    }

    private class HasJobToFail : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize(new HasExistingJob());

            var jobFailedMessage = fixture.Build<SimpleTransit.MessageInfrastructure.Messages.JobFailed>()
                .With(x => x.JobId, fixture.Create<Job>().JobId)
                .Create();

            fixture.Inject(jobFailedMessage);
        }
    }

    private class WithCustomJobStateUpdate : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize(new HasExistingJob());

            var updateCustomJobStateMessage = fixture.Build<SimpleTransit.MessageInfrastructure.Messages.UpdateCustomJobState>()
                .With(x => x.JobId, fixture.Create<Job>().JobId)
                .Create();

            fixture.Inject(updateCustomJobStateMessage);
        }
    }

    private class WithRecurringJobs : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            var dbContext = fixture.Create<JobStateDataContext>();

            var jobType = new JobType { Name = "The Job Type" };
            var recurringJobType = new JobType { Name = "The Recurring Job Type" };

            var job = new Job
            {
                JobId = Guid.NewGuid(),
                Description = "Some Description",
                Created = DateTime.UtcNow,
                JobType = jobType
            };

            var recurringJob = new Job
            {
                JobId = Guid.NewGuid(),
                Description = "Recurring Job",
                Created = DateTime.UtcNow,
                JobType = recurringJobType,
                IsRecurringJob = true,
                Interval = TimeSpan.FromSeconds(5)
            };

            dbContext.Jobs.AddRange(job, recurringJob);
            dbContext.JobTypes.AddRange(jobType, recurringJobType);

            dbContext.SaveChanges();

            fixture.Inject(recurringJob);
        }
    }

    private class WithOldJobs : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            var dbContext = fixture.Create<JobStateDataContext>();

            var jobType = new JobType { Name = "The Job Type" };

            var job = new Job
            {
                JobId = Guid.NewGuid(),
                Description = "Some Description",
                Created = DateTime.UtcNow,
                JobType = jobType,
            };

            var oldJob = new Job
            {
                JobId = Guid.NewGuid(),
                Description = "Some Description",
                Created = DateTime.UtcNow.AddHours(-2),
                JobType = jobType
            };
            dbContext.Jobs.AddRange(job, oldJob);
            dbContext.JobTypes.AddRange(jobType);

            dbContext.SaveChanges();

            fixture.Inject(job);
        }
    }

    private class WithHungJobs : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            var dbContext = fixture.Create<JobStateDataContext>();

            var jobType = new JobType { Name = "The Job Type" };

            var createdJob = new Job
            {
                JobId = Guid.NewGuid(),
                Description = "Some Description",
                Created = DateTime.UtcNow.AddHours(-5),
                JobType = jobType,
                CurrentState = JobState.Created
            };

            var startedJob = new Job
            {
                JobId = Guid.NewGuid(),
                Description = "Some Description",
                Created = DateTime.UtcNow.AddHours(-5),
                Started = DateTime.UtcNow.AddHours(-5),
                JobType = jobType,
                CurrentState = JobState.Started
            };
            dbContext.Jobs.AddRange(createdJob, startedJob);
            dbContext.JobTypes.AddRange(jobType);

            dbContext.SaveChanges();

            fixture.Inject(createdJob);
        }
    }
}