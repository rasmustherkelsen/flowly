namespace SimpleTransit.MessageInfrastructure.Messages;

internal record UpdateCustomJobState(Guid JobId, object CustomState);