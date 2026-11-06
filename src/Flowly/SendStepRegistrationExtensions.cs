using Flowly.MessageInfrastructure.SendSteps;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

/// <summary>
///     Provides extension methods for registering send steps in the Flowly framework.
/// </summary>
public static class SendStepRegistrationExtensions
{
    /// <summary>
    ///     Registers a send step that runs before every message matching the step's message filter leaves the sender. The step is
    ///     registered as a scoped service and resolved from a fresh scope created for each send, so it does not share the caller's
    ///     scope. The first registered step is the outermost.
    /// </summary>
    /// <remarks>
    ///     A step runs for <see cref="IMessageSender" />, <see cref="IMessageRecorder" />, <see cref="IEventSender" />,
    ///     <see cref="IMessageCaller" /> and <c>IJobMessageSender</c> sends of the configuration whose message type matches the
    ///     filter. Flowly's internal messages never pass through send steps. See <see cref="SendStep{TMessageFilter}" /> for the
    ///     exception, short-circuit and scope semantics.
    /// </remarks>
    /// <param name="flowlyBuilder">A valid IFlowlyBuilder instance</param>
    /// <typeparam name="TSendStep">
    ///     The step type. Must derive from <see cref="SendStep{TMessageFilter}" /> or <see cref="SendStep" />.
    /// </typeparam>
    /// <returns>The same <see cref="IFlowlyBuilder" /> for further configuration</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <typeparamref name="TSendStep" /> does not derive from <see cref="SendStep{TMessageFilter}" />.
    /// </exception>
    public static IFlowlyBuilder AddSendStep<TSendStep>(this IFlowlyBuilder flowlyBuilder)
        where TSendStep : class, ISendStep
    {
        var registration = new SendStepRegistration(typeof(TSendStep), SendStepRegistration.ResolveFilterType(typeof(TSendStep)));

        flowlyBuilder.Services.AddScoped<TSendStep>();
        flowlyBuilder.Services.AddSingleton(registration);

        return flowlyBuilder;
    }
}
