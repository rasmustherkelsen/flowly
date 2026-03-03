using Flowly.Jobs.Model;

namespace Flowly.Jobs.Messages;

public record JobIsAlive(JobId JobId, DateTimeOffset TimeStamp);