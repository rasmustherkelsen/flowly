namespace Flowly.MessageInfrastructure.Callers;

internal interface ICallSubmitter<TMessage, TReturn>
    where TMessage : class, IReturns<TReturn>
    where TReturn : class
{
    Task<TReturn> Submit(TMessage message, CancellationToken cancellationToken);
}
