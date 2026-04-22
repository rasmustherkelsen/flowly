namespace Flowly;

/// <summary>
///     Attribute to specify a custom name for an event type. If not provided, the event name will be derived from the
///     class name by removing the "Event" suffix (if present) and converting to kebab-case.
/// </summary>
/// <param name="name"></param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class EventNameAttribute(string name) : Attribute
{
    /// <summary>
    ///     The custom name for the event type. If not provided, the event name will be derived from the class name by removing
    /// </summary>
    public string Name { get; } = name;
}