using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;

namespace Flowly.Jobs.Tests.Messages;

public class FlowlysysJobFailedMessageTests
{
    public class Constructor
    {
        [Fact]
        public void StoresAllValues()
        {
            var jobId = new JobId();
            var timeStamp = new DateTime(2026, 4, 19);

            var jobFailed = new FlowlysysJobFailedMessage(jobId, "handler threw", timeStamp);

            Assert.Equal(jobId, jobFailed.JobId);
            Assert.Equal("handler threw", jobFailed.FaultReason);
            Assert.Equal(timeStamp, jobFailed.TimeStamp);
        }
    }

    public class Equality
    {
        [Fact]
        public void DifferentFaultReason_NotEqual()
        {
            var jobId = new JobId();
            var timeStamp = new DateTime(2026, 4, 19);

            var first = new FlowlysysJobFailedMessage(jobId, "reason a", timeStamp);
            var second = new FlowlysysJobFailedMessage(jobId, "reason b", timeStamp);

            Assert.NotEqual(first, second);
        }
    }
}
