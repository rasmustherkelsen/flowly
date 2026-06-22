using System.Diagnostics;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.Tests.MessageInfrastructure.Telemetry;

public class ActivityTagExtensionsTests
{
    public class ApplyTagsFrom
    {
        [Fact]
        public void WithNullActivity_DoesNotThrow()
        {
            Activity? activity = null;

            var exception = Record.Exception(() => activity.ApplyTagsFrom(new TaggedMessage("x")));

            Assert.Null(exception);
        }

        [Fact]
        public void WithNullMessage_DoesNotSetAnyTags()
        {
            using var activity = new Activity("test").Start();

            activity.ApplyTagsFrom(null);

            Assert.Empty(activity.Tags);
        }

        [Fact]
        public void WithMessageThatDoesNotImplementInterface_DoesNotSetAnyTags()
        {
            using var activity = new Activity("test").Start();

            activity.ApplyTagsFrom(new PlainMessage("x"));

            Assert.Empty(activity.Tags);
        }

        [Fact]
        public void WithMessageThatImplementsInterface_SetsAllReturnedTags()
        {
            using var activity = new Activity("test").Start();

            activity.ApplyTagsFrom(new TaggedMessage("order-123"));

            Assert.Equal("order-123", activity.GetTagItem("order.id"));
        }

        [Fact]
        public void WithMessageReturningMultipleTags_SetsAllOfThem()
        {
            using var activity = new Activity("test").Start();

            activity.ApplyTagsFrom(new MultiTagMessage("order-1", "customer-2"));

            Assert.Equal("order-1", activity.GetTagItem("order.id"));
            Assert.Equal("customer-2", activity.GetTagItem("customer.id"));
        }
    }

    private record PlainMessage(string Value);

    private record TaggedMessage(string OrderId) : IOpenTelemetryTagsProvider
    {
        public IEnumerable<KeyValuePair<string, object?>> GetOpenTelemetryTags() =>
            [new("order.id", OrderId)];
    }

    private record MultiTagMessage(string OrderId, string CustomerId) : IOpenTelemetryTagsProvider
    {
        public IEnumerable<KeyValuePair<string, object?>> GetOpenTelemetryTags() =>
        [
            new("order.id", OrderId),
            new("customer.id", CustomerId),
        ];
    }
}
