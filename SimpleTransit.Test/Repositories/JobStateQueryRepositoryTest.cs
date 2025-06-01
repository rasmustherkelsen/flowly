using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimpleTransit.DatabaseModel.JobStateDatabase;
using SimpleTransit.Repositories;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.Repositories;

public class JobStateQueryRepositoryTest
{
    public class Exists
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobStateQueryRepositoryForTest), typeof(HasExistingJobs))]
        internal async Task MustReturnTrueWhenJobExists(JobStateQueryRepository jobStateQueryRepository, Job job)
        {
            Assert.True(await jobStateQueryRepository.Exists(job.JobId));
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobStateQueryRepositoryForTest))]
        internal async Task MustReturnFalseWhenJobDoesNotExist(JobStateQueryRepository jobStateQueryRepository)
        {
            Assert.False(await jobStateQueryRepository.Exists(Guid.NewGuid()));
        }
        
        [Theory, AutoDataWithCustomization(typeof(SetupJobStateQueryRepositoryForTest), typeof(HasExistingJobs))]
        internal async Task MustReturnFalseWhenJobIsNotRecurring(JobStateQueryRepository jobStateQueryRepository, Job job)
        {
            Assert.False(await jobStateQueryRepository.Exists(job.JobId, true));
        }
        
        [Theory, AutoDataWithCustomization(typeof(SetupJobStateQueryRepositoryForTest), typeof(HasExistingRecurringJobs))]
        internal async Task MustReturnTrueWhenRecurringJobExists(JobStateQueryRepository jobStateQueryRepository, Job job)
        {
            Assert.True(await jobStateQueryRepository.Exists(job.JobId, true));
        }
    }

    public class Query
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobStateQueryRepositoryForTest), typeof(HasExistingJobs))]
        internal async Task MustReturnAllJobs(JobStateQueryRepository jobStateQueryRepository)
        {
            var result = await jobStateQueryRepository.Query();

            Assert.Equal(2, result.Count);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobStateQueryRepositoryForTest), typeof(HasExistingJobs))]
        internal async Task MustReturnJobById(JobStateQueryRepository jobStateQueryRepository, Job job)
        {
            var result = await jobStateQueryRepository.Query(JobQuery.ById(job.JobId));

            Assert.Collection(result, j => Assert.Equal(job.JobId, j.JobId));
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobStateQueryRepositoryForTest), typeof(HasExistingJobs))]
        internal async Task MustReturnJobByJobType(JobStateQueryRepository jobStateQueryRepository, Job job)
        {
            var result = await jobStateQueryRepository.Query(JobQuery.ByJobType("The Recurring Job Type"));

            Assert.Collection(result, j => Assert.NotEqual(job.JobId, j.JobId));
        }
    }

    private class SetupJobStateQueryRepositoryForTest : ICustomization
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

    private class HasExistingRecurringJobs() : HasExistingJobsBase(true);

    private class HasExistingJobs : HasExistingJobsBase;
        
    private abstract class HasExistingJobsBase(bool isRecurringJob = false) : ICustomization
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

            fixture.Inject(isRecurringJob ? recurringJob : job);
        }
    }
}