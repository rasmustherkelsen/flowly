namespace Flowly.MessageInfrastructure.Telemetry;

/// <summary>
///     Opt-in interface that a message contract can implement to attach custom OpenTelemetry span tags.
///     Flowly automatically calls <see cref="GetOpenTelemetryTags" /> and sets the returned key-value pairs on
///     the active <see cref="System.Diagnostics.Activity" /> whenever a producer span (<c>flowly.send</c> /
///     <c>flowly.event.raise</c>) or a consumer span (<c>flowly.handle</c> / <c>flowly.event.handle</c>) is
///     started for that message type.
/// </summary>
/// <remarks>
///     Tags appear on the span in Jaeger (and any OTel-compatible backend) and can be used to filter and
///     correlate traces by business-level values such as order IDs, customer IDs, or tenant identifiers.
///     Avoid returning high-cardinality values as metric dimensions — tags are traces-only.
/// </remarks>
/// <example>
///     <code>
///     public record SubmitOrderMessage(string OrderId, string CustomerId) : IOpenTelemetryTagsProvider
///     {
///         public IEnumerable&lt;KeyValuePair&lt;string, object?&gt;&gt; GetOpenTelemetryTags() =>
///         [
///             new("order.id", OrderId),
///             new("customer.id", CustomerId),
///         ];
///     }
///     </code>
/// </example>
public interface IOpenTelemetryTagsProvider
{
    /// <summary>
    ///     Returns the key-value pairs to attach as tags on the OpenTelemetry span for this message.
    /// </summary>
    /// <returns>
    ///     A sequence of <see cref="KeyValuePair{TKey,TValue}" /> where the key is the tag name and the value
    ///     is the tag value (any scalar type accepted by
    ///     <see cref="System.Diagnostics.Activity.SetTag(string, object?)" />).
    /// </returns>
    IEnumerable<KeyValuePair<string, object?>> GetOpenTelemetryTags();
}
