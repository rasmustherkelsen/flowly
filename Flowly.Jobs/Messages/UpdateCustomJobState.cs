using Flowly.Jobs.Model;

namespace Flowly.Jobs.Messages;

internal record UpdateCustomJobState(JobId JobId, object CustomState);