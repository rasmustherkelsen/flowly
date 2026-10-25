using Flowly.AzureEventHubs.Tests.Fakes;

namespace Flowly.AzureEventHubs.Tests;

public class EventHubsErrorThrottleTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public class TryReport
    {
        [Fact]
        public void FirstError_IsReportedWithoutSuppressedErrors()
        {
            var eventHubsErrorThrottle = new EventHubsErrorThrottle(new ManualTimeProvider(), Interval);

            Assert.True(eventHubsErrorThrottle.TryReport(out var suppressed));
            Assert.Equal(0, suppressed);
        }

        [Fact]
        public void ErrorWithinTheInterval_IsSuppressed()
        {
            var timeProvider = new ManualTimeProvider();
            var eventHubsErrorThrottle = new EventHubsErrorThrottle(timeProvider, Interval);
            eventHubsErrorThrottle.TryReport(out _);

            timeProvider.Advance(TimeSpan.FromSeconds(29));

            Assert.False(eventHubsErrorThrottle.TryReport(out _));
        }

        [Fact]
        public void ErrorAfterTheInterval_IsReportedWithTheNumberOfSuppressedErrors()
        {
            var timeProvider = new ManualTimeProvider();
            var eventHubsErrorThrottle = new EventHubsErrorThrottle(timeProvider, Interval);
            eventHubsErrorThrottle.TryReport(out _);
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            eventHubsErrorThrottle.TryReport(out _);
            eventHubsErrorThrottle.TryReport(out _);

            timeProvider.Advance(TimeSpan.FromSeconds(20));

            Assert.True(eventHubsErrorThrottle.TryReport(out var suppressed));
            Assert.Equal(2, suppressed);
        }

        [Fact]
        public void AfterAReport_CountsSuppressedErrorsFromZeroAgain()
        {
            var timeProvider = new ManualTimeProvider();
            var eventHubsErrorThrottle = new EventHubsErrorThrottle(timeProvider, Interval);
            eventHubsErrorThrottle.TryReport(out _);
            eventHubsErrorThrottle.TryReport(out _);
            timeProvider.Advance(Interval);
            eventHubsErrorThrottle.TryReport(out _);

            timeProvider.Advance(Interval);

            Assert.True(eventHubsErrorThrottle.TryReport(out var suppressed));
            Assert.Equal(0, suppressed);
        }
    }
}
