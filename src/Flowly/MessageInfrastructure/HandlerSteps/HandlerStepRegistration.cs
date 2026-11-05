namespace Flowly.MessageInfrastructure.HandlerSteps;

internal sealed record HandlerStepRegistration(Type StepType, Type FilterType)
{
    public static Type ResolveFilterType(Type stepType)
    {
        for (var baseType = stepType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(HandlerStep<>))
            {
                return baseType.GetGenericArguments()[0];
            }
        }

        throw new ArgumentException(
            $"'{stepType}' is not a handler step: it must derive from {nameof(HandlerStep)} or {nameof(HandlerStep)}<TMessageFilter>.",
            nameof(stepType));
    }
}
