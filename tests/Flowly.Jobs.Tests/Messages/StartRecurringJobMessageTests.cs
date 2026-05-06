using Flowly.Jobs.Messages;

namespace Flowly.Jobs.Tests.Messages;

public class StartRecurringJobMessageTests
{
    public class Constructor
    {
        [Fact]
        public void StoresJobId()
        {
            var jobId = Guid.NewGuid();

            var startRecurringJobMessage = new StartRecurringJobMessage(jobId);

            Assert.Equal(jobId, startRecurringJobMessage.JobId);
        }
    }
}
