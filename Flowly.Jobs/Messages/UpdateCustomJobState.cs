namespace Flowly.Jobs.Messages;

internal record UpdateCustomJobState(Guid JobId, object CustomState);