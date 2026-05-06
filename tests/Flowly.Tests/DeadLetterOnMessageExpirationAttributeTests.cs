namespace Flowly.Tests;

public class DeadLetterOnMessageExpirationAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void WithEnabledTrue_SetsEnabled()
        {
            var deadLetterOnMessageExpirationAttribute = new DeadLetterOnMessageExpirationAttribute(enabled: true);

            Assert.True(deadLetterOnMessageExpirationAttribute.Enabled);
        }

        [Fact]
        public void WithEnabledFalse_SetsEnabled()
        {
            var deadLetterOnMessageExpirationAttribute = new DeadLetterOnMessageExpirationAttribute(enabled: false);

            Assert.False(deadLetterOnMessageExpirationAttribute.Enabled);
        }
    }
}
