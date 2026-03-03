namespace Flowly.MessageInfrastructure.Registration;

public static class FlowlyModuleExtensions
{
    public static IFlowlyBuilder UseModule<TModule>(this IFlowlyBuilder builder) where TModule : IFlowlyConfiguration, new()
    {
        var module = new TModule();
        module.Configure(builder);
        return builder;
    }
}