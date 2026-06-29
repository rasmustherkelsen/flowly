namespace Flowly.MessageInfrastructure;

/// <summary>
///     The default <see cref="ITopologyNameResolver" /> implementation. Derives broker names from .NET type names using
///     kebab-case conversion, with optional overrides via <see cref="QueueNameAttribute" /> and
///     <see cref="EventNameAttribute" />. PascalCase type names are split on uppercase boundaries and joined with
///     <c>-</c>; a trailing <c>Message</c> or <c>Event</c> suffix is stripped before conversion
///     (e.g. <c>ProcessOrderMessage</c> → <c>process-order</c>, <c>OrderPlacedEvent</c> → <c>order-placed</c>).
/// </summary>
public class KebabCaseTopologyNameResolver() : SeparatedCaseTopologyNameResolver("-");
