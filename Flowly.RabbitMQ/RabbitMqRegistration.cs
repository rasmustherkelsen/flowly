using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

public static class RabbitMqRegistration
{
    public static IFlowlyBuilder UseRabbitMq(this IFlowlyBuilder flowlyBuilder)
        => flowlyBuilder.UseRabbitMq("amqp://guest:guest@localhost:5672/");

    public static IFlowlyBuilder UseRabbitMq(this IFlowlyBuilder flowlyBuilder, string connection)
    {
        flowlyBuilder.Services.AddSingleton<IConnection>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var uri = configuration.GetConnectionString(connection) ?? connection;

            var factory = new ConnectionFactory
            {
                Uri = new Uri(uri),
                AutomaticRecoveryEnabled = true
            };

            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        flowlyBuilder.Services.AddSingleton<IMessageBusClient, RabbitMqMessageBusClient>();
        flowlyBuilder.Services.AddTransient<IMessagingTopologyCreator, RabbitMqMessagingTopologyCreator>();

        return flowlyBuilder;
    }
}
