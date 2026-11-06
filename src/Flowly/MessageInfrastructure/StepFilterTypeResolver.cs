namespace Flowly.MessageInfrastructure;

internal static class StepFilterTypeResolver
{
    public static Type Resolve(Type stepType, Type openGenericStepBaseType)
    {
        for (var baseType = stepType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == openGenericStepBaseType)
            {
                return baseType.GetGenericArguments()[0];
            }
        }

        var baseTypeName = openGenericStepBaseType.Name.Split('`')[0];

        throw new ArgumentException(
            $"'{stepType}' is not a {GetDescription(baseTypeName)}: it must derive from {baseTypeName} or {baseTypeName}<TMessageFilter>.",
            nameof(stepType));
    }

    private static string GetDescription(string baseTypeName)
        => baseTypeName switch
        {
            nameof(HandlerStep) => "handler step",
            nameof(SendStep) => "send step",
            _ => baseTypeName
        };
}
