using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;

namespace Flowly.Jobs.Tests.Messages;

public class UpdateCustomJobStateTests
{
    public class Constructor
    {
        [Fact]
        public void StoresJobIdAndCustomState()
        {
            var jobId = new JobId();
            var customState = new { Step = 2, Total = 5 };

            var updateCustomJobState = new UpdateCustomJobState(jobId, customState);

            Assert.Equal(jobId, updateCustomJobState.JobId);
            Assert.Same(customState, updateCustomJobState.CustomState);
        }
    }
}
