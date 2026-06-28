namespace Flowly.MessageInfrastructure;

/// <summary>
///     An <see cref="ITopologyNameResolver" /> implementation that derives broker names from .NET type names using
///     dot-case conversion, with optional overrides via <see cref="QueueNameAttribute" /> and
///     <see cref="EventNameAttribute" />. PascalCase type names are split on uppercase boundaries and joined with
///     <c>.</c>; a trailing <c>Message</c> or <c>Event</c> suffix is stripped before conversion
///     (e.g. <c>ProcessOrderMessage</c> → <c>process.order</c>, <c>OrderPlacedEvent</c> → <c>order.placed</c>).
/// </summary>
/// <remarks>
///     Dot-case naming is idiomatic in RabbitMQ, where queues and exchanges are commonly named using period-delimited
///     segments (e.g. <c>order.processing.created</c>). Register this resolver via
///     <see cref="FlowlyOptions.WithTopologyNameResolver{TResolver}" />.
/// </remarks>
public class DotCaseTopologyNameResolver() : SeparatedCaseTopologyNameResolver(".");