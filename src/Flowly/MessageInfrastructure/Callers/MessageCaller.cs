using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Callers;

internal class MessageCaller(IServiceProvider serviceProvider) : IMessageCaller
{
    public Task<TReturn> Call<TMessage, TReturn>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class, IReturns<TReturn>
        where TReturn : class
    {
        var submitter = serviceProvider.GetRequiredService<ICallSubmitter<TMessage, TReturn>>();
        return submitter.Submit(message, cancellationToken);
    }
}
