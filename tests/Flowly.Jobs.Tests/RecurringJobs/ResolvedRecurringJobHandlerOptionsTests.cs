using Flowly.Jobs.RecurringJobs;

namespace Flowly.Jobs.Tests.RecurringJobs;

public class ResolvedRecurringJobHandlerOptionsTests
{
    public class Constructor
    {
        [Fact]
        public void StoresBothProperties()
        {
            var resolvedRecurringJobHandlerOptions = new ResolvedRecurringJobHandlerOptions(
                JobDescription: "nightly cleanup",
                CronExpression: "0 2 * * *");

            Assert.Equal("nightly cleanup", resolvedRecurringJobHandlerOptions.JobDescription);
            Assert.Equal("0 2 * * *", resolvedRecurringJobHandlerOptions.CronExpression);
        }
    }

    public class Equality
    {
        [Fact]
        public void TwoInstancesWithSameValues_AreEqual()
        {
            var first = new ResolvedRecurringJobHandlerOptions("x", "y");
            var second = new ResolvedRecurringJobHandlerOptions("x", "y");

            Assert.Equal(first, second);
        }
    }
}
