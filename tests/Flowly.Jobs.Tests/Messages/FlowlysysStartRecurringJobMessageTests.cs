using Flowly.Jobs.Messages;

namespace Flowly.Jobs.Tests.Messages;

public class FlowlysysStartRecurringJobMessageTests
{
    public class Constructor
    {
        [Fact]
        public void StoresJobId()
        {
            var jobId = Guid.NewGuid();

            var startRecurringJobMessage = new FlowlysysStartRecurringJobMessage(jobId);

            Assert.Equal(jobId, startRecurringJobMessage.JobId);
        }
    }
}
