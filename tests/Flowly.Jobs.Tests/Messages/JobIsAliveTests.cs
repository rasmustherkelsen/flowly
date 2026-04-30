using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;

namespace Flowly.Jobs.Tests.Messages;

public class JobIsAliveTests
{
    public class Constructor
    {
        [Fact]
        public void StoresJobIdAndTimestamp()
        {
            var jobId = new JobId();
            var timeStamp = DateTimeOffset.Parse("2026-04-19T12:00:00Z");

            var jobIsAlive = new JobIsAlive(jobId, timeStamp);

            Assert.Equal(jobId, jobIsAlive.JobId);
            Assert.Equal(timeStamp, jobIsAlive.TimeStamp);
        }
    }
}
