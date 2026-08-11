namespace Flowly;

/// <summary>
///     Attribute to set the <see cref="StartPosition" /> a <see cref="MessageStreamHandler{TMessage}" /> begins
///     consuming from, for the two constant-expressible positions. Apply to the <strong>handler</strong> class,
///     alongside attributes like <see cref="BatchProcessingAttribute" /> and <see cref="RetryPolicyAttribute" />.
///     <para>
///         Only <see cref="StreamStartPositionKind.First" /> and <see cref="StreamStartPositionKind.Last" /> are
///         supported here — <see cref="StartPosition.Offset" /> and <see cref="StartPosition.Timestamp" /> require a
///         runtime value (a timestamp is routinely computed relative to "now", which cannot be a compile-time
///         attribute argument) and must be set via <see cref="MessageStreamHandler{TMessage}.Configure" /> instead.
///     </para>
///     <para>
///         If the handler also overrides <see cref="MessageStreamHandler{TMessage}.Configure" /> and sets
///         <see cref="MessageStreamHandlerOptions.StartPosition" /> there, the <c>Configure</c> value takes
///         precedence over this attribute — the same precedence <see cref="BatchProcessingAttribute" /> has.
///     </para>
/// </summary>
/// <param name="kind">Which constant start position to use.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StreamStartPositionAttribute(StreamStartPositionKind kind) : Attribute
{
    /// <summary>
    ///     The constant start position this attribute resolves to.
    /// </summary>
    public StreamStartPositionKind Kind { get; } = kind;
}

/// <summary>
///     The constant, compile-time-expressible <see cref="StartPosition" /> values that
///     <see cref="StreamStartPositionAttribute" /> can represent.
/// </summary>
public enum StreamStartPositionKind
{
    /// <summary>
    ///     Corresponds to <see cref="StartPosition.First" />.
    /// </summary>
    First,

    /// <summary>
    ///     Corresponds to <see cref="StartPosition.Last" />.
    /// </summary>
    Last
}
