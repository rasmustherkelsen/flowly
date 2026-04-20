using Flowly.MessageInfrastructure.RecurringJobs;

namespace Flowly.Tests.MessageInfrastructure.RecurringJobs;

public class RecurringJobAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void SetsJobDescription()
        {
            var recurringJobAttribute = new RecurringJobAttribute("Daily cleanup", "0 2 * * *");

            Assert.Equal("Daily cleanup", recurringJobAttribute.JobDescription);
        }

        [Fact]
        public void SetsCronExpression()
        {
            var recurringJobAttribute = new RecurringJobAttribute("Daily cleanup", "0 2 * * *");

            Assert.Equal("0 2 * * *", recurringJobAttribute.CronExpression);
        }
    }
}
