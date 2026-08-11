namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Retention limits for a single stream queue, resolved from <see cref="StreamRetentionAttribute" /> on the
///     message contract and <see cref="MessageStreamHandlerOptions" /> overrides.
/// </summary>
/// <param name="MaxAgeSeconds">The maximum age of retained messages in seconds, or <see langword="null" /> for no age limit.</param>
/// <param name="MaxLengthBytes">The maximum total size of the stream in bytes, or <see langword="null" /> for no size limit.</param>
public readonly record struct StreamRetentionSettings(int? MaxAgeSeconds, long? MaxLengthBytes);