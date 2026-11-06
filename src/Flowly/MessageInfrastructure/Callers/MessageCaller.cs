using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Callers;

internal class MessageCaller(IServiceProvider serviceProvider) : IMessageCaller
{
    public Task<TReturn> Call<TMessage, TReturn>(
        TMessage message,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? customProperties = null)
        where TMessage : class, IReturns<TReturn>
        where TReturn : class
        => serviceProvider.GetRequiredService<ICallSubmitter<TMessage, TReturn>>().Submit(message, cancellationToken, customProperties);
}
