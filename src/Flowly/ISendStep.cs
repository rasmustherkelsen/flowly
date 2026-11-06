namespace Flowly;

/// <summary>
///     Marker interface implemented by every send step so registration can constrain on "any send step". Do not implement it
///     directly; derive from <see cref="SendStep{TMessageFilter}" /> or <see cref="SendStep" /> instead.
/// </summary>
public interface ISendStep;
