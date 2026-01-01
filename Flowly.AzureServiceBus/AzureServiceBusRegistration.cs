using Microsoft.Extensions.DependencyInjection;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus;

public static class AzureServiceBusRegistration
{
    public static ISimpleTransitBuilder UseAzureServiceBus(this ISimpleTransitBuilder simpleTransitBuilder, string connectionString)
    {
        simpleTransitBuilder.Services.AddSingleton<IMessageBusClient, MessageBusClient>();
        return simpleTransitBuilder;
    }
}