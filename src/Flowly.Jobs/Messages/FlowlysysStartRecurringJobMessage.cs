using Flowly.MessageInfrastructure.SendSteps;

namespace Flowly.Jobs.Messages;

internal record FlowlysysStartRecurringJobMessage(Guid JobId) : IFlowlySystemMessage;