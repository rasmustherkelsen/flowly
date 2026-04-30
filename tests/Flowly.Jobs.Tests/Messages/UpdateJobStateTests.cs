using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;

namespace Flowly.Jobs.Tests.Messages;

public class UpdateJobStateTests
{
    public class Constructor
    {
        [Fact]
        public void WithRequiredArguments_RetryAttemptDefaultsToZero()
        {
            var jobId = new JobId();
            var timeStamp = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            var updateJobState = new UpdateJobState(jobId, JobState.Started, timeStamp);

            Assert.Equal(jobId, updateJobState.JobId);
            Assert.Equal(JobState.Started, updateJobState.JobState);
            Assert.Equal(timeStamp, updateJobState.TimeStamp);
            Assert.Equal(0, updateJobState.RetryAttempt);
        }

        [Fact]
        public void WithRetryAttempt_StoresValue()
        {
            var jobId = new JobId();

            var updateJobState = new UpdateJobState(jobId, JobState.Failed, DateTime.UtcNow, RetryAttempt: 3);

            Assert.Equal(3, updateJobState.RetryAttempt);
        }
    }

    public class Equality
    {
        [Fact]
        public void TwoInstancesWithSameValues_AreEqual()
        {
            var jobId = new JobId();
            var timeStamp = new DateTime(2026, 4, 19);

            var first = new UpdateJobState(jobId, JobState.Completed, timeStamp, 2);
            var second = new UpdateJobState(jobId, JobState.Completed, timeStamp, 2);

            Assert.Equal(first, second);
        }
    }
}
