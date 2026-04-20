using Flowly.Jobs.Services;

namespace Flowly.Jobs.Tests.Services;

public class RecurringJobInfoTests
{
    public class Construction
    {
        [Fact]
        public void PopulatesAllProperties()
        {
            var jobId = Guid.NewGuid();
            var created = DateTimeOffset.UtcNow;
            var lastStarted = created.AddMinutes(1);
            var lastCompleted = lastStarted.AddSeconds(10);

            var recurringJobInfo = new RecurringJobInfo(
                JobId: jobId,
                JobTypeName: "NightlyJob",
                Description: "cleans up",
                CronExpression: "0 2 * * *",
                Created: created,
                LastStarted: lastStarted,
                LastCompleted: lastCompleted);

            Assert.Equal(jobId, recurringJobInfo.JobId);
            Assert.Equal("NightlyJob", recurringJobInfo.JobTypeName);
            Assert.Equal("cleans up", recurringJobInfo.Description);
            Assert.Equal("0 2 * * *", recurringJobInfo.CronExpression);
            Assert.Equal(created, recurringJobInfo.Created);
            Assert.Equal(lastStarted, recurringJobInfo.LastStarted);
            Assert.Equal(lastCompleted, recurringJobInfo.LastCompleted);
        }

        [Fact]
        public void AllowsNullLastStartedAndLastCompleted()
        {
            var recurringJobInfo = new RecurringJobInfo(
                JobId: Guid.NewGuid(),
                JobTypeName: "T",
                Description: "d",
                CronExpression: "* * * * *",
                Created: DateTimeOffset.UtcNow,
                LastStarted: null,
                LastCompleted: null);

            Assert.Null(recurringJobInfo.LastStarted);
            Assert.Null(recurringJobInfo.LastCompleted);
        }
    }
}
