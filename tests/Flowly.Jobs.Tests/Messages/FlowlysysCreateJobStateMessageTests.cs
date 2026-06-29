using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;

namespace Flowly.Jobs.Tests.Messages;

public class FlowlysysCreateJobStateMessageTests
{
    public class Constructor
    {
        [Fact]
        public void StoresAllValues()
        {
            var jobId = new JobId();
            var timeStamp = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            var createJobState = new FlowlysysCreateJobStateMessage(jobId, "OrderJob", "Processing order", timeStamp);

            Assert.Equal(jobId, createJobState.JobId);
            Assert.Equal("OrderJob", createJobState.JobTypeName);
            Assert.Equal("Processing order", createJobState.Description);
            Assert.Equal(timeStamp, createJobState.TimeStamp);
        }
    }

    public class Equality
    {
        [Fact]
        public void TwoInstancesWithSameValues_AreEqual()
        {
            var jobId = new JobId();
            var timeStamp = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            var first = new FlowlysysCreateJobStateMessage(jobId, "OrderJob", "desc", timeStamp);
            var second = new FlowlysysCreateJobStateMessage(jobId, "OrderJob", "desc", timeStamp);

            Assert.Equal(first, second);
        }

        [Fact]
        public void DifferentJobTypeName_NotEqual()
        {
            var jobId = new JobId();
            var timeStamp = new DateTime(2026, 4, 19);

            var first = new FlowlysysCreateJobStateMessage(jobId, "A", "desc", timeStamp);
            var second = new FlowlysysCreateJobStateMessage(jobId, "B", "desc", timeStamp);

            Assert.NotEqual(first, second);
        }
    }
}
