namespace SimpleTransit.MessageInfrastructure.Messages;

internal record JobFailed(Guid JobId, string FaultReason, DateTime TimeStamp);