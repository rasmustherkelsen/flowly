using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.HandlerSteps;

internal sealed class HandlerStepRunner(IEnumerable<HandlerStepRegistration> registrations) : IHandlerStepRunner
{
    public static readonly HandlerStepRunner None = new([]);

    private readonly HandlerStepRegistration[] _registrations = registrations.ToArray();
    private readonly ConcurrentDictionary<Type, Type[]> _stepTypesByMessageType = new();
    private Func<Type, Type[]>? _findMatchingStepTypes;

    public Task Run(
        object message,
        MessageProperties properties,
        Type handlerType,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken,
        HandlerStepDelegate handler)
    {
        var stepTypes = _stepTypesByMessageType.GetOrAdd(message.GetType(), _findMatchingStepTypes ??= FindMatchingStepTypes);

        if (stepTypes.Length == 0)
        {
            return handler();
        }

        var next = handler;

        for (var index = stepTypes.Length - 1; index >= 0; index--)
        {
            var step = (IHandlerStepInvoker)scopedServices.GetRequiredService(stepTypes[index]);
            var inner = next;
            next = () => step.Invoke(message, properties, handlerType, cancellationToken, inner);
        }

        return next();
    }

    private Type[] FindMatchingStepTypes(Type messageType)
        => _registrations
            .Where(registration => registration.FilterType.IsAssignableFrom(messageType))
            .Select(registration => registration.StepType)
            .ToArray();
}
