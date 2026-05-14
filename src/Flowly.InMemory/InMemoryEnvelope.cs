namespace Flowly.InMemory;

internal record InMemoryEnvelope(
    string MessageId,
    string RawBody,
    Dictionary<string, object> ApplicationProperties,
    DateTimeOffset EnqueuedTime,
    DateTimeOffset? ScheduledDeliveryTime = null,
    object? OriginalMessage = null);
