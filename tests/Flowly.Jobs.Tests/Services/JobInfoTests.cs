using Flowly.Jobs.Model;
using Flowly.Jobs.Services;

namespace Flowly.Jobs.Tests.Services;

public class JobInfoTests
{
    public class Construction
    {
        [Fact]
        public void PopulatesAllProperties()
        {
            var jobIdentifier = Guid.NewGuid();
            var created = DateTimeOffset.UtcNow;
            var started = created.AddSeconds(1);
            var completed = started.AddSeconds(5);

            var jobInfo = new JobInfo(
                JobIdentifier: jobIdentifier,
                JobTypeName: "MyJob",
                Description: "does stuff",
                CurrentState: JobState.Completed,
                Created: created,
                Started: started,
                Completed: completed,
                FaultReason: null,
                RetryAttempt: 2);

            Assert.Equal(jobIdentifier, jobInfo.JobIdentifier);
            Assert.Equal("MyJob", jobInfo.JobTypeName);
            Assert.Equal("does stuff", jobInfo.Description);
            Assert.Equal(JobState.Completed, jobInfo.CurrentState);
            Assert.Equal(created, jobInfo.Created);
            Assert.Equal(started, jobInfo.Started);
            Assert.Equal(completed, jobInfo.Completed);
            Assert.Null(jobInfo.FaultReason);
            Assert.Equal(2, jobInfo.RetryAttempt);
        }

        [Fact]
        public void AllowsNullStartedCompletedAndFaultReason()
        {
            var jobInfo = new JobInfo(
                JobIdentifier: Guid.NewGuid(),
                JobTypeName: "Job",
                Description: "",
                CurrentState: JobState.Created,
                Created: DateTimeOffset.UtcNow,
                Started: null,
                Completed: null,
                FaultReason: null,
                RetryAttempt: 0);

            Assert.Null(jobInfo.Started);
            Assert.Null(jobInfo.Completed);
            Assert.Null(jobInfo.FaultReason);
        }
    }

    public class Equality
    {
        [Fact]
        public void TwoRecordsWithSameValues_AreEqual()
        {
            var id = Guid.NewGuid();
            var created = DateTimeOffset.UtcNow;

            var first = new JobInfo(id, "T", "d", JobState.Started, created, null, null, null, 0);
            var second = new JobInfo(id, "T", "d", JobState.Started, created, null, null, null, 0);

            Assert.Equal(first, second);
        }
    }
}
