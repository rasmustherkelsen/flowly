using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Telemetry;

internal static class ActivityContextParser
{
    /// <summary>
    ///     Parses the W3C <c>traceparent</c> and <c>tracestate</c> headers from <paramref name="properties" /> into an
    ///     <see cref="ActivityContext" />. Returns <see langword="default" /> when no traceparent is present or the value
    ///     is malformed.
    /// </summary>
    /// <param name="properties">Message properties containing the trace propagation headers.</param>
    /// <returns>
    ///     A valid remote <see cref="ActivityContext" /> when the traceparent header is present and well-formed;
    ///     otherwise <see langword="default" />.
    /// </returns>
    internal static ActivityContext Parse(MessageProperties properties)
    {
        if (properties.Traceparent is null) return default;

        return ActivityContext.TryParse(properties.Traceparent, properties.Tracestate, isRemote: true, out var context)
            ? context
            : default;
    }
}
