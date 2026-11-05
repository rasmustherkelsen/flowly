using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.SendSteps;

internal sealed class SendStepRunner(IEnumerable<SendStepRegistration> registrations, IServiceScopeFactory scopeFactory) : ISendStepRunner
{
    public static readonly SendStepRunner None = new([], new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

    private readonly SendStepRegistration[] _registrations = registrations.ToArray();
    private readonly ConcurrentDictionary<Type, Type[]> _stepTypesByMessageType = new();
    private Func<Type, Type[]>? _findMatchingStepTypes;

    public Task Run(object message, CancellationToken cancellationToken, SendStepDelegate send)
    {
        if (message is IFlowlySystemMessage)
        {
            return send();
        }

        var stepTypes = _stepTypesByMessageType.GetOrAdd(message.GetType(), _findMatchingStepTypes ??= FindMatchingStepTypes);

        return stepTypes.Length == 0 ? send() : RunSteps(stepTypes, message, cancellationToken, send);
    }

    private async Task RunSteps(Type[] stepTypes, object message, CancellationToken cancellationToken, SendStepDelegate send)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var next = send;

        for (var index = stepTypes.Length - 1; index >= 0; index--)
        {
            var step = (ISendStepInvoker)scope.ServiceProvider.GetRequiredService(stepTypes[index]);
            var inner = next;
            next = () => step.Invoke(message, cancellationToken, inner);
        }

        await next();
    }

    private Type[] FindMatchingStepTypes(Type messageType)
        => _registrations
            .Where(registration => registration.FilterType.IsAssignableFrom(messageType))
            .Select(registration => registration.StepType)
            .ToArray();
}
