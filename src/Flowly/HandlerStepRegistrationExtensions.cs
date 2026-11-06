using Flowly.MessageInfrastructure.HandlerSteps;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

/// <summary>
///     Provides extension methods for registering handler steps in the Flowly framework.
/// </summary>
public static class HandlerStepRegistrationExtensions
{
    /// <summary>
    ///     Registers a handler step that wraps the user's <c>Handle</c> call for every handler whose message matches the step's
    ///     message filter. The step is registered as a scoped service, so it shares the per-message scope, and its scoped services,
    ///     with the handler. The first registered step is the outermost.
    /// </summary>
    /// <remarks>
    ///     A step applies to every message, event, call and job handler registered in the same configuration whose message type
    ///     matches the filter, whether the step is registered before or after the handler. There is no per-handler attachment.
    ///     Batch, message stream and recurring handlers are not covered. Registering the same step type twice runs it twice and a
    ///     step whose filter matches no registered handler never runs; both are logged as warnings at host start-up. See
    ///     <see cref="HandlerStep{TMessageFilter}" /> for the execution, exception and short-circuit semantics.
    /// </remarks>
    /// <param name="flowlyBuilder">A valid IFlowlyBuilder instance</param>
    /// <typeparam name="THandlerStep">
    ///     The step type. Must derive from <see cref="HandlerStep{TMessageFilter}" /> or <see cref="HandlerStep" />.
    /// </typeparam>
    /// <returns>The same <see cref="IFlowlyBuilder" /> for further configuration</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <typeparamref name="THandlerStep" /> does not derive from <see cref="HandlerStep{TMessageFilter}" />.
    /// </exception>
    public static IFlowlyBuilder AddHandlerStep<THandlerStep>(this IFlowlyBuilder flowlyBuilder)
        where THandlerStep : class, IHandlerStep
    {
        var registration = new HandlerStepRegistration(typeof(THandlerStep), HandlerStepRegistration.ResolveFilterType(typeof(THandlerStep)));

        flowlyBuilder.Services.AddScoped<THandlerStep>();
        flowlyBuilder.Services.AddSingleton(registration);

        return flowlyBuilder;
    }
}
