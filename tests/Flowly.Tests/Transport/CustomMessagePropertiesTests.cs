using System.Text;
using Flowly.Transport;

namespace Flowly.Tests.Transport;

public class CustomMessagePropertiesTests
{
    public class IsReserved
    {
        [Theory]
        [InlineData("flowly-retry-count")]
        [InlineData("flowly-anything")]
        [InlineData("Flowly-Mixed-Case")]
        [InlineData("traceparent")]
        [InlineData("TraceState")]
        public void ForFlowlyAndTraceKeys_ReturnsTrue(string key)
            => Assert.True(CustomMessageProperties.IsReserved(key));

        [Theory]
        [InlineData("tenant")]
        [InlineData("x-tenant")]
        [InlineData("my-flowly-key")]
        [InlineData("flowly")]
        public void ForApplicationKeys_ReturnsFalse(string key)
            => Assert.False(CustomMessageProperties.IsReserved(key));
    }

    public class Validate
    {
        [Fact]
        public void WithNull_DoesNotThrow()
            => CustomMessageProperties.Validate(null, "customProperties");

        [Fact]
        public void WithApplicationKeys_DoesNotThrow()
            => CustomMessageProperties.Validate(new Dictionary<string, string> { ["tenant"] = "a", ["user"] = "b" }, "customProperties");

        [Fact]
        public void WithReservedKey_ThrowsArgumentExceptionNamingKeyAndParameter()
        {
            var exception = Assert.Throws<ArgumentException>(() => CustomMessageProperties.Validate(new Dictionary<string, string> { ["flowly-retry-count"] = "9" }, "customProperties"));

            Assert.Equal("customProperties", exception.ParamName);
            Assert.Contains("flowly-retry-count", exception.Message);
        }

        [Theory]
        [InlineData("traceparent")]
        [InlineData("tracestate")]
        public void WithTraceKey_ThrowsArgumentException(string key)
            => Assert.Throws<ArgumentException>(() => CustomMessageProperties.Validate(new Dictionary<string, string> { [key] = "v" }, "customProperties"));

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        public void WithBlankKey_ThrowsArgumentException(string key)
            => Assert.Throws<ArgumentException>(() => CustomMessageProperties.Validate(new Dictionary<string, string> { [key] = "v" }, "customProperties"));

        [Fact]
        public void WithNullValue_ThrowsArgumentExceptionNamingKey()
        {
            var exception = Assert.Throws<ArgumentException>(() => CustomMessageProperties.Validate(new Dictionary<string, string> { ["tenant"] = null! }, "customProperties"));

            Assert.Contains("tenant", exception.Message);
        }
    }

    public class Read
    {
        [Fact]
        public void WithNull_ReturnsEmpty()
            => Assert.Same(CustomMessageProperties.Empty, CustomMessageProperties.Read(null));

        [Fact]
        public void WithNoEntries_ReturnsEmpty()
            => Assert.Same(CustomMessageProperties.Empty, CustomMessageProperties.Read(new Dictionary<string, object?>()));

        [Fact]
        public void WithStringValue_ReturnsEntry()
        {
            var customProperties = CustomMessageProperties.Read(new Dictionary<string, object?> { ["tenant"] = "contoso" });

            Assert.Equal("contoso", customProperties["tenant"]);
        }

        [Fact]
        public void WithByteArrayValue_DecodesUtf8()
        {
            var customProperties = CustomMessageProperties.Read(new Dictionary<string, object?> { ["tenant"] = Encoding.UTF8.GetBytes("æøå") });

            Assert.Equal("æøå", customProperties["tenant"]);
        }

        [Fact]
        public void WithNonTextValues_SkipsThem()
        {
            var customProperties = CustomMessageProperties.Read(new Dictionary<string, object?> { ["count"] = 3, ["flag"] = true, ["missing"] = null });

            Assert.Empty(customProperties);
        }

        [Fact]
        public void WithReservedKeys_SkipsThem()
        {
            var customProperties = CustomMessageProperties.Read(new Dictionary<string, object?>
            {
                ["flowly-target-subscription"] = "sub",
                ["traceparent"] = "00-abc",
                ["tracestate"] = "a=b",
                ["tenant"] = "contoso"
            });

            Assert.Equal(["tenant"], customProperties.Keys);
        }
    }
}
