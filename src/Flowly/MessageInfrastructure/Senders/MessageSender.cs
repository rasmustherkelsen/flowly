using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Senders;

internal class MessageSender(IServiceProvider serviceProvider) : IMessageSender
{
    public async Task Send<TMessage>(TMessage message, CancellationToken cancellationToken = default)
    {
        var messageSubmitter = serviceProvider.GetRequiredService<IMessageSubmitter<TMessage>>();
        await messageSubmitter.Submit(message, cancellationToken);
    }
}