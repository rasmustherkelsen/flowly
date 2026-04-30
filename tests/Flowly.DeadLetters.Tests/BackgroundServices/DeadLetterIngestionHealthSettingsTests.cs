using Flowly.DeadLetters.BackgroundServices;

namespace Flowly.DeadLetters.Tests.BackgroundServices;

public class DeadLetterIngestionHealthSettingsTests
{
    public class Construction
    {
        [Fact]
        public void PopulatesBothProperties()
        {
            var checkInterval = TimeSpan.FromSeconds(30);
            var stallThreshold = TimeSpan.FromMinutes(5);

            var deadLetterIngestionHealthSettings = new DeadLetterIngestionHealthSettings(checkInterval, stallThreshold);

            Assert.Equal(checkInterval, deadLetterIngestionHealthSettings.CheckInterval);
            Assert.Equal(stallThreshold, deadLetterIngestionHealthSettings.StallThreshold);
        }
    }

    public class Equality
    {
        [Fact]
        public void TwoInstancesWithSameValues_AreEqual()
        {
            var first = new DeadLetterIngestionHealthSettings(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));
            var second = new DeadLetterIngestionHealthSettings(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));

            Assert.Equal(first, second);
        }

        [Fact]
        public void TwoInstancesWithDifferentValues_AreNotEqual()
        {
            var first = new DeadLetterIngestionHealthSettings(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));
            var second = new DeadLetterIngestionHealthSettings(TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(5));

            Assert.NotEqual(first, second);
        }
    }
}
