namespace Flowly.MessageInfrastructure.SendSteps;

internal sealed record SendStepRegistration(Type StepType, Type FilterType)
{
    public static Type ResolveFilterType(Type stepType) => StepFilterTypeResolver.Resolve(stepType, typeof(SendStep<>));
}
