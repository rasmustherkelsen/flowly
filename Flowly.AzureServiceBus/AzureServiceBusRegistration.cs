using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.Configuration;

namespace Flowly.AzureServiceBus;

public static class AzureServiceBusRegistration
{
    private const string EmulatorConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    public static IFlowlyBuilder UseAzureServiceBus(this IFlowlyBuilder flowlyBuilder) =>
        flowlyBuilder.UseAzureServiceBusWithConnectionString(EmulatorConnectionString);

    public static IFlowlyBuilder UseAzureServiceBus(this IFlowlyBuilder flowlyBuilder, string connection)
    {
        flowlyBuilder.Services.AddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString(connection) ?? connection;

            return new ServiceBusClient(connectionString);
        });

        flowlyBuilder.Services.AddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString(connection) ?? connection;

            return new ServiceBusAdministrationClient(connectionString);
        });

        flowlyBuilder.Services.AddSingleton<IMessageBusClient, MessageBusClient>();
        flowlyBuilder.Services.AddTransient<IMessagingTopologyCreator, MessagingTopologyCreator>();
        return flowlyBuilder;
    }

    private static IFlowlyBuilder UseAzureServiceBusWithConnectionString(this IFlowlyBuilder flowlyBuilder, string connectionString)
    {
        flowlyBuilder.Services.AddSingleton(_ => new ServiceBusClient(connectionString));
        flowlyBuilder.Services.AddSingleton(_ => new ServiceBusAdministrationClient(connectionString));
        flowlyBuilder.Services.AddSingleton<IMessageBusClient, MessageBusClient>();
        flowlyBuilder.Services.AddTransient<IMessagingTopologyCreator, MessagingTopologyCreator>();

        return flowlyBuilder;
    }
}