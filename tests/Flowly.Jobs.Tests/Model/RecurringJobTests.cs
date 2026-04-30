using Flowly.Jobs.Model;

namespace Flowly.Jobs.Tests.Model;

public class RecurringJobTests
{
    public class IsDue
    {
        [Fact]
        public void WhenStartedButNotCompleted_ReturnsFalse()
        {
            var recurringJob = new RecurringJob(
                jobId: Guid.NewGuid(),
                jobTypeName: "JobType",
                description: "desc",
                cronExpression: "* * * * *",
                created: DateTimeOffset.UtcNow.AddHours(-1),
                started: DateTimeOffset.UtcNow.AddMinutes(-10),
                completed: null);

            Assert.False(recurringJob.IsDue());
        }

        [Fact]
        public void WhenNextOccurrenceInThePast_ReturnsTrue()
        {
            var recurringJob = new RecurringJob(
                jobId: Guid.NewGuid(),
                jobTypeName: "JobType",
                description: "desc",
                cronExpression: "* * * * *",
                created: DateTimeOffset.UtcNow.AddHours(-1),
                started: null,
                completed: null);

            Assert.True(recurringJob.IsDue());
        }

        [Fact]
        public void WhenCompletedLongAgoAndCronMatchesEveryMinute_ReturnsTrue()
        {
            var recurringJob = new RecurringJob(
                jobId: Guid.NewGuid(),
                jobTypeName: "JobType",
                description: "desc",
                cronExpression: "* * * * *",
                created: DateTimeOffset.UtcNow.AddHours(-2),
                started: DateTimeOffset.UtcNow.AddHours(-2),
                completed: DateTimeOffset.UtcNow.AddMinutes(-5));

            Assert.True(recurringJob.IsDue());
        }

        [Fact]
        public void WhenCreatedInTheFuture_ReturnsFalse()
        {
            var farFuture = DateTimeOffset.UtcNow.AddYears(10);

            var recurringJob = new RecurringJob(
                jobId: Guid.NewGuid(),
                jobTypeName: "JobType",
                description: "desc",
                cronExpression: "0 2 * * *",
                created: farFuture,
                started: null,
                completed: null);

            Assert.False(recurringJob.IsDue());
        }

        [Fact]
        public void WithSixFieldCronExpression_ParsesAsIncludeSeconds()
        {
            var recurringJob = new RecurringJob(
                jobId: Guid.NewGuid(),
                jobTypeName: "JobType",
                description: "desc",
                cronExpression: "* * * * * *",
                created: DateTimeOffset.UtcNow.AddHours(-1),
                started: null,
                completed: null);

            Assert.True(recurringJob.IsDue());
        }
    }

    public class Constructor
    {
        [Fact]
        public void StoresAllValues()
        {
            var jobId = Guid.NewGuid();
            var created = DateTimeOffset.UtcNow.AddHours(-2);
            var started = DateTimeOffset.UtcNow.AddHours(-1);
            var completed = DateTimeOffset.UtcNow.AddMinutes(-5);

            var recurringJob = new RecurringJob(
                jobId: jobId,
                jobTypeName: "JobType",
                description: "desc",
                cronExpression: "0 2 * * *",
                created: created,
                started: started,
                completed: completed);

            Assert.Equal(jobId, recurringJob.JobId);
            Assert.Equal("JobType", recurringJob.JobTypeName);
            Assert.Equal("desc", recurringJob.Description);
            Assert.Equal("0 2 * * *", recurringJob.CronExpression);
            Assert.Equal(created, recurringJob.Created);
            Assert.Equal(started, recurringJob.Started);
            Assert.Equal(completed, recurringJob.Completed);
        }
    }
}
