namespace Flowly.DeadLetters.Repositories;

internal record PurgedDeadLetter(string MessageId, string QueueName, string MessageProperties);
