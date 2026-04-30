namespace Flowly.Jobs.Tests;

public class RecurringJobHandlerOptionsTests
{
    public class DefaultValues
    {
        [Fact]
        public void AllPropertiesAreNull()
        {
            var recurringJobHandlerOptions = new RecurringJobHandlerOptions();

            Assert.Null(recurringJobHandlerOptions.JobDescription);
            Assert.Null(recurringJobHandlerOptions.CronExpression);
        }
    }

    public class Setters
    {
        [Fact]
        public void StoresAllValues()
        {
            var recurringJobHandlerOptions = new RecurringJobHandlerOptions
            {
                JobDescription = "nightly",
                CronExpression = "0 0 * * *"
            };

            Assert.Equal("nightly", recurringJobHandlerOptions.JobDescription);
            Assert.Equal("0 0 * * *", recurringJobHandlerOptions.CronExpression);
        }
    }
}
