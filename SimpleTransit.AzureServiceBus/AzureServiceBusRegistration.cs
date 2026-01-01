using Microsoft.Extensions.DependencyInjection;
using SimpleTransit.MessageInfrastructure.Registration;
using SimpleTransit.MessagingAbstractions;

namespace SimpleTransit.AzureServiceBus;

public static class AzureServiceBusRegistration
{
    public static ISimpleTransitBuilder UseAzureServiceBus(this ISimpleTransitBuilder simpleTransitBuilder, string connectionString)
    {
        simpleTransitBuilder.Services.AddSingleton<IMessageBusClient, MessageBusClient>();
        return simpleTransitBuilder;
    }
}