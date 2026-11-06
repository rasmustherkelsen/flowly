namespace Flowly;

/// <summary>
///     Marker interface implemented by every handler step so registration can constrain on "any step". Do not implement it
///     directly; derive from <see cref="HandlerStep{TMessageFilter}" /> or <see cref="HandlerStep" /> instead.
/// </summary>
public interface IHandlerStep;
