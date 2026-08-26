using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Flowly.AzureServiceBus.Tests;

public class AzureServiceBusConnectionLifetimeTests
{
    private const string EmulatorConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    public class StopAsync
    {
        [Fact]
        public async Task DisposesTheMessageBusClient()
        {
            var serviceBusClient = new ServiceBusClient(EmulatorConnectionString);
            var administrationClient = new ServiceBusAdministrationClient(EmulatorConnectionString);
            var messageBusClient = new MessageBusClient(serviceBusClient, administrationClient, null);
            var azureServiceBusConnectionLifetime = new AzureServiceBusConnectionLifetime(messageBusClient);

            await azureServiceBusConnectionLifetime.StopAsync(CancellationToken.None);
        }
    }
}
