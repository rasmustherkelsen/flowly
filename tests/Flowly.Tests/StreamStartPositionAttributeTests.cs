namespace Flowly.Tests;

public class StreamStartPositionAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void WithFirst_ExposesFirstKind()
        {
            var attribute = new StreamStartPositionAttribute(StreamStartPositionKind.First);

            Assert.Equal(StreamStartPositionKind.First, attribute.Kind);
        }

        [Fact]
        public void WithLast_ExposesLastKind()
        {
            var attribute = new StreamStartPositionAttribute(StreamStartPositionKind.Last);

            Assert.Equal(StreamStartPositionKind.Last, attribute.Kind);
        }

        [Fact]
        public void IsReadableFromDecoratedHandlerClass()
        {
            var attribute = (StreamStartPositionAttribute?)Attribute.GetCustomAttribute(typeof(DecoratedHandler), typeof(StreamStartPositionAttribute));

            Assert.NotNull(attribute);
            Assert.Equal(StreamStartPositionKind.Last, attribute.Kind);
        }
    }

    [StreamStartPosition(StreamStartPositionKind.Last)]
    private class DecoratedHandler;
}
