using Flowly.Jobs.BackgroundServices;

namespace Flowly.Jobs.Tests.BackgroundServices;

public class RecurringJobSettingsTests
{
    public class Constructor
    {
        [Fact]
        public void StoresJobDescriptionSessionNameAndCronExpression()
        {
            var recurringJobSettings = new RecurringJobHandlerBackgroundService<TestRecurringJob>.RecurringJobSettings(
                "Runs every minute",
                "test-session",
                "* * * * *");

            Assert.Equal("Runs every minute", recurringJobSettings.JobDescription);
            Assert.Equal("test-session", recurringJobSettings.SessionName);
            Assert.Equal("* * * * *", recurringJobSettings.CronExpression);
        }

        [Fact]
        public void WithFiveSegmentCronExpression_DoesNotThrow()
        {
            var recurringJobSettings = new RecurringJobHandlerBackgroundService<TestRecurringJob>.RecurringJobSettings(
                "Daily at midnight",
                "session",
                "0 0 * * *");

            Assert.Equal("0 0 * * *", recurringJobSettings.CronExpression);
        }

        [Fact]
        public void WithSixSegmentCronExpression_TreatsFirstSegmentAsSeconds()
        {
            var recurringJobSettings = new RecurringJobHandlerBackgroundService<TestRecurringJob>.RecurringJobSettings(
                "Every 5 seconds",
                "session",
                "*/5 * * * * *");

            Assert.Equal("*/5 * * * * *", recurringJobSettings.CronExpression);
        }

        [Fact]
        public void WithInvalidCronExpression_ThrowsArgumentException()
        {
            var argumentException = Assert.Throws<ArgumentException>(() =>
                new RecurringJobHandlerBackgroundService<TestRecurringJob>.RecurringJobSettings(
                    "Bad",
                    "session",
                    "this-is-not-a-cron-expression"));

            Assert.Equal("cronExpression", argumentException.ParamName);
        }

        [Fact]
        public void WithEmptyCronExpression_ThrowsArgumentException()
        {
            var argumentException = Assert.Throws<ArgumentException>(() =>
                new RecurringJobHandlerBackgroundService<TestRecurringJob>.RecurringJobSettings(
                    "Empty",
                    "session",
                    string.Empty));

            Assert.Equal("cronExpression", argumentException.ParamName);
        }

        [Fact]
        public void WithFourSegmentCronExpression_ThrowsArgumentException()
        {
            var argumentException = Assert.Throws<ArgumentException>(() =>
                new RecurringJobHandlerBackgroundService<TestRecurringJob>.RecurringJobSettings(
                    "Too few segments",
                    "session",
                    "0 0 * *"));

            Assert.Equal("cronExpression", argumentException.ParamName);
        }

        [Fact]
        public void WithSevenSegmentCronExpression_ThrowsArgumentException()
        {
            var argumentException = Assert.Throws<ArgumentException>(() =>
                new RecurringJobHandlerBackgroundService<TestRecurringJob>.RecurringJobSettings(
                    "Too many segments",
                    "session",
                    "0 0 0 * * * *"));

            Assert.Equal("cronExpression", argumentException.ParamName);
        }

        [Fact]
        public void WithSixSegmentInvalidCronExpression_ThrowsArgumentException()
        {
            var argumentException = Assert.Throws<ArgumentException>(() =>
                new RecurringJobHandlerBackgroundService<TestRecurringJob>.RecurringJobSettings(
                    "Invalid seconds",
                    "session",
                    "?? * * * * *"));

            Assert.Equal("cronExpression", argumentException.ParamName);
        }
    }

    private sealed class TestRecurringJob : RecurringJobHandler
    {
        public override Task Handle(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
