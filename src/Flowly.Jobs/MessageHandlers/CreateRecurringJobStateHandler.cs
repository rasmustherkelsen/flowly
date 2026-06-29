using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Repositories;

namespace Flowly.Jobs.MessageHandlers;

internal class CreateRecurringJobStateHandler(IJobStateRepository jobStateRepository, ICustomJobStateRepository customJobStateRepository) : MessageHandler<FlowlysysCreateRecurringJobStateMessage>
{
    public override void Configure(HandlerQueueOptions options)
    {
        options.MaxConcurrentCalls = Environment.ProcessorCount;
    }

    public override async Task Handle(IMessageContext<FlowlysysCreateRecurringJobStateMessage> messageContext)
    {
        var newJobId = new JobId();

        await Task.WhenAll(
            jobStateRepository.CreateRecurringJobState(messageContext.Message, newJobId),
            customJobStateRepository.CreateCustomJobState(newJobId));
    }
}