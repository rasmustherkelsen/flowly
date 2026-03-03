namespace Flowly.MessageInfrastructure.Registration;

public record DeferredQueueRegistration(
	string QueueName,
	bool RequiresSession = false,
	TimeSpan? DefaultMessageTimeToLive = null,
	bool? DeadLetterOnMessageExpiration = null,
	TimeSpan? LockDuration = null);