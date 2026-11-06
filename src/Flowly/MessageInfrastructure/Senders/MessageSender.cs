using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Senders;

internal class MessageSender(IServiceProvider serviceProvider) : IMessageSender
{
    public Task Send<TMessage>(TMessage message, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string>? customProperties = null)
        => serviceProvider.GetRequiredService<IMessageSubmitter<TMessage>>().Submit(message, cancellationToken, customProperties: customProperties);
}