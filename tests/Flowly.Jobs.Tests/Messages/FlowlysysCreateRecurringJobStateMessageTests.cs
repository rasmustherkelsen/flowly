using Flowly.Jobs.Messages;

namespace Flowly.Jobs.Tests.Messages;

public class FlowlysysCreateRecurringJobStateMessageTests
{
    public class Constructor
    {
        [Fact]
        public void StoresAllValues()
        {
            var timeStamp = new DateTime(2026, 4, 19);

            var createRecurringJobState = new FlowlysysCreateRecurringJobStateMessage("NightlyJob", "Nightly cleanup", timeStamp, "0 2 * * *");

            Assert.Equal("NightlyJob", createRecurringJobState.JobTypeName);
            Assert.Equal("Nightly cleanup", createRecurringJobState.Description);
            Assert.Equal(timeStamp, createRecurringJobState.TimeStamp);
            Assert.Equal("0 2 * * *", createRecurringJobState.CronExpression);
        }
    }
}
