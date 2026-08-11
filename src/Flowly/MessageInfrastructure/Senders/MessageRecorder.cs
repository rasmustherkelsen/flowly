using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Senders;

internal class MessageRecorder(IServiceProvider serviceProvider) : IMessageRecorder
{
    public async Task Record<TMessage>(TMessage message, CancellationToken cancellationToken = default)
    {
        var messageSubmitter = serviceProvider.GetRequiredService<IMessageSubmitter<TMessage>>();
        await messageSubmitter.Submit(message, cancellationToken);
    }
}
