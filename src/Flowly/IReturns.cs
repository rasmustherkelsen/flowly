namespace Flowly;

/// <summary>
///     Marker interface that a call message must implement to declare the type of its response. The Flowly framework
///     uses this contract to route the return value from a <see cref="CallHandler{TMessage,TReturn}" /> back to the
///     originating <see cref="IMessageCaller" />.
/// </summary>
/// <typeparam name="TReturn">The response type produced by the remote call handler.</typeparam>
public interface IReturns<TReturn> where TReturn : class
{
}
