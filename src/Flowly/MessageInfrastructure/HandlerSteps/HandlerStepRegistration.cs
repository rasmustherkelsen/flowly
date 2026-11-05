namespace Flowly.MessageInfrastructure.HandlerSteps;

internal sealed record HandlerStepRegistration(Type StepType, Type FilterType)
{
    public static Type ResolveFilterType(Type stepType) => StepFilterTypeResolver.Resolve(stepType, typeof(HandlerStep<>));
}
