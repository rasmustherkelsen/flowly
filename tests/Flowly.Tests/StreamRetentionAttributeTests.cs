namespace Flowly.Tests;

public class StreamRetentionAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void WithBothValues_ExposesBoth()
        {
            var attribute = new StreamRetentionAttribute(3600, 1_000_000);

            Assert.Equal(3600, attribute.MaxAgeSeconds);
            Assert.Equal(1_000_000, attribute.MaxLengthBytes);
        }

        [Fact]
        public void WithoutValues_ExposesNulls()
        {
            var attribute = new StreamRetentionAttribute();

            Assert.Null(attribute.MaxAgeSeconds);
            Assert.Null(attribute.MaxLengthBytes);
        }

        [Fact]
        public void WithOnlyMaxAge_ExposesNullMaxLength()
        {
            var attribute = new StreamRetentionAttribute(maxAgeSeconds: 60);

            Assert.Equal(60, attribute.MaxAgeSeconds);
            Assert.Null(attribute.MaxLengthBytes);
        }

        [Fact]
        public void WithOnlyMaxLength_ExposesNullMaxAge()
        {
            var attribute = new StreamRetentionAttribute(maxLengthBytes: 5_000_000);

            Assert.Null(attribute.MaxAgeSeconds);
            Assert.Equal(5_000_000, attribute.MaxLengthBytes);
        }

        [Fact]
        public void IsReadableFromDecoratedMessageContract()
        {
            var attribute = (StreamRetentionAttribute?)Attribute.GetCustomAttribute(typeof(RetainedMessage), typeof(StreamRetentionAttribute));

            Assert.NotNull(attribute);
            Assert.Equal(604800, attribute.MaxAgeSeconds);
        }
    }

    [StreamRetention(maxAgeSeconds: 604800)]
    private record RetainedMessage;
}
