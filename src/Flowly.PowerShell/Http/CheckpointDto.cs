namespace Flowly.PowerShell.Http;

/// <summary>Mirrors the JSON shape returned by the Management API's list-checkpoints action.</summary>
public sealed record CheckpointDto(string QueueName, string ConsumerName, int? Partition, long? Position, DateTimeOffset UpdatedAtUtc);
