namespace Messages;

/// <summary>
/// A sample stream message with a single entry number.
/// </summary>
public record MyStreamMessage(long EntryNumber);