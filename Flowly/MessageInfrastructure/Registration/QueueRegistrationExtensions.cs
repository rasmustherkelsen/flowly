using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

public static class QueueRegistrationExtensions
{
    public static IFlowlyBuilder AddQueueRegistration(this IFlowlyBuilder flowlyBuilder, DeferredQueueRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(registration.QueueName))
        {
            return flowlyBuilder;
        }

        flowlyBuilder.Services.AddSingleton(registration);
        return flowlyBuilder;
    }

    public static IFlowlyBuilder AddQueueRegistration(this IFlowlyBuilder flowlyBuilder, string queueName, bool requiresSession = false)
    {
        return flowlyBuilder.AddQueueRegistration(new DeferredQueueRegistration(queueName, requiresSession));
    }
}