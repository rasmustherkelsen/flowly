using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.Configuration;

namespace Flowly.AzureServiceBus;

public static class AzureServiceBusRegistration
{
    public static IFlowlyBuilder UseAzureServiceBus(this IFlowlyBuilder flowlyBuilder, string connectionStringName)
    {
        flowlyBuilder.Services.AddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString(connectionStringName)
                                   ?? throw new InvalidOperationException(
                                       $"Connection string '{connectionStringName}' not found.");

            return new ServiceBusClient(connectionString);
        });

        flowlyBuilder.Services.AddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString(connectionStringName)
                                   ?? throw new InvalidOperationException(
                                       $"Connection string '{connectionStringName}' not found.");

            return new ServiceBusAdministrationClient(connectionString);
        });

        flowlyBuilder.Services.AddSingleton<IMessageBusClient, MessageBusClient>();
        flowlyBuilder.Services.AddTransient<IMessagingTopologyCreator, MessagingTopologyCreator>();
        return flowlyBuilder;
    }
}