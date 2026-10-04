using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

/// <summary>
///     Provides <see cref="AddMessageStreamCheckpoint{TCheckpoint}" />, a shorthand for registering a
///     user-implemented <see cref="MessageStreamCheckpoint{TMessage}" />.
/// </summary>
public static class MessageStreamCheckpointRegistrationExtensions
{
    /// <summary>
    ///     Registers <typeparamref name="TCheckpoint" /> as the <see cref="MessageStreamCheckpoint{TMessage}" /> for
    ///     whichever message type it targets — sugar for
    ///     <c>services.AddSingleton&lt;MessageStreamCheckpoint&lt;TMessage&gt;, TCheckpoint&gt;()</c> that infers
    ///     <c>TMessage</c> from <typeparamref name="TCheckpoint" />'s base type instead of requiring it to be spelled
    ///     out separately.
    /// </summary>
    /// <typeparam name="TCheckpoint">
    ///     A concrete class inheriting <see cref="MessageStreamCheckpoint{TMessage}" /> for some message type.
    /// </typeparam>
    /// <param name="flowlyBuilder">The Flowly builder to register with.</param>
    /// <returns>The <see cref="IFlowlyBuilder" /> for further configuration.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <typeparamref name="TCheckpoint" /> does not inherit a closed
    ///     <see cref="MessageStreamCheckpoint{TMessage}" />.
    /// </exception>
    public static IFlowlyBuilder AddMessageStreamCheckpoint<TCheckpoint>(this IFlowlyBuilder flowlyBuilder)
        where TCheckpoint : class
    {
        var checkpointBaseType = ResolveCheckpointBaseType(typeof(TCheckpoint))
            ?? throw new InvalidOperationException(
                $"{typeof(TCheckpoint).Name} must inherit MessageStreamCheckpoint<TMessage> for some message type " +
                "to be registered via AddMessageStreamCheckpoint().");

        flowlyBuilder.Services.AddSingleton(checkpointBaseType, typeof(TCheckpoint));

        return flowlyBuilder;
    }

    private static Type? ResolveCheckpointBaseType(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(MessageStreamCheckpoint<>))
                return current;
        }

        return null;
    }
}
