using Flowly.Jobs.Model;
using Flowly.Jobs.Repositories;

namespace Flowly.Jobs.Tests.Repositories;

public class JobInformationTests
{
    public class Construction
    {
        [Fact]
        public void PopulatesAllProperties()
        {
            var jobId = Guid.NewGuid();
            var created = DateTimeOffset.UtcNow;
            var started = created.AddSeconds(1);
            var completed = started.AddSeconds(5);

            var jobInformation = new JobInformation(
                JobId: jobId,
                JobTypeName: "NightlyJob",
                CurrentState: JobState.Completed,
                Description: "does things",
                Created: created,
                Started: started,
                Completed: completed,
                FaultReason: null,
                IsRecurringJob: true,
                CronExpression: "0 * * * *",
                CustomJobState: "{\"step\":3}");

            Assert.Equal(jobId, jobInformation.JobId);
            Assert.Equal("NightlyJob", jobInformation.JobTypeName);
            Assert.Equal(JobState.Completed, jobInformation.CurrentState);
            Assert.Equal("does things", jobInformation.Description);
            Assert.Equal(created, jobInformation.Created);
            Assert.Equal(started, jobInformation.Started);
            Assert.Equal(completed, jobInformation.Completed);
            Assert.Null(jobInformation.FaultReason);
            Assert.True(jobInformation.IsRecurringJob);
            Assert.Equal("0 * * * *", jobInformation.CronExpression);
            Assert.Equal("{\"step\":3}", jobInformation.CustomJobState);
        }

        [Fact]
        public void WhenNotRecurring_AllowsNullCronAndCustomState()
        {
            var jobInformation = new JobInformation(
                JobId: Guid.NewGuid(),
                JobTypeName: "J",
                CurrentState: JobState.Created,
                Description: "",
                Created: DateTimeOffset.UtcNow,
                Started: null,
                Completed: null,
                FaultReason: null,
                IsRecurringJob: false,
                CronExpression: null,
                CustomJobState: null);

            Assert.False(jobInformation.IsRecurringJob);
            Assert.Null(jobInformation.CronExpression);
            Assert.Null(jobInformation.CustomJobState);
        }

        [Fact]
        public void WithFailedState_CapturesFaultReason()
        {
            var jobInformation = new JobInformation(
                JobId: Guid.NewGuid(),
                JobTypeName: "J",
                CurrentState: JobState.Failed,
                Description: "",
                Created: DateTimeOffset.UtcNow,
                Started: DateTimeOffset.UtcNow,
                Completed: null,
                FaultReason: "boom",
                IsRecurringJob: false,
                CronExpression: null,
                CustomJobState: null);

            Assert.Equal(JobState.Failed, jobInformation.CurrentState);
            Assert.Equal("boom", jobInformation.FaultReason);
        }
    }
}
