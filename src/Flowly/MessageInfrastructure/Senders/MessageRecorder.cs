using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Senders;

internal class MessageRecorder(IServiceProvider serviceProvider) : IMessageRecorder
{
    public Task Record<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default,
        string? partitionKey = null,
        IReadOnlyDictionary<string, string>? customProperties = null)
        => serviceProvider.GetRequiredService<IMessageSubmitter<TMessage>>().Submit(message, cancellationToken, partitionKey, customProperties);
}
