namespace SimpleTransit.MessageInfrastructure.Messages;

internal record CreateJobState(Guid JobId, string JobTypeName, string Description, DateTime TimeStamp);