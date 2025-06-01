using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SimpleTransit.AzureServiceBusWrappers;
using SimpleTransit.MessageInfrastructure.BackgroundServices;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.BackgroundServices;

public class ServiceBusMessageBatchHandlerBackgroundServiceTest
{
    public class ExecuteAsync
    {
        [Theory, AutoDataWithCustomization(typeof(SetupServiceBusMessageBatchHandlerBackgroundServiceForTest))]
        internal async Task MustHandleReceivedMessages(
              ServiceBusMessageBatchHandlerBackgroundService<MyMessage> serviceBusMessageBatchHandlerBackgroundService,
              IServiceBusReceiver serviceBusReceiver,
              IBatchMessageHandler<MyMessage> messageHandler,
              CancellationToken cancellationToken)
        {
            await serviceBusMessageBatchHandlerBackgroundService.StartAsync(cancellationToken);

            await serviceBusReceiver.Received(1).ReceiveMessagesAsync(Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
            await messageHandler.Received(1).Handle(Arg.Is<BatchMessageContext<MyMessage>>(ctx => ctx.Messages.Count == 1));
        }
    }

    private class SetupServiceBusMessageBatchHandlerBackgroundServiceForTest : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize(new AutoNSubstituteCustomization());

            var serviceBusClient = fixture.Create<IServiceBusClient>();
            fixture.Inject(serviceBusClient);

            var serviceBusReceiver = fixture.Create<IServiceBusReceiver>();
            serviceBusClient.CreateReceiver(Arg.Any<string>()).Returns(serviceBusReceiver);
            fixture.Inject(serviceBusReceiver);

            var receivedMessage = MessageBusHelper.CreateServiceBusReceivedMessage(new MyMessage());

            serviceBusReceiver.ReceiveMessagesAsync(Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns([receivedMessage]);

            var messageHandler = fixture.Create<IBatchMessageHandler<MyMessage>>();
            fixture.Inject(messageHandler);

            var serviceScope = Substitute.For<IServiceScope>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(typeof(IBatchMessageHandler<MyMessage>)).Returns(messageHandler);
            serviceScope.ServiceProvider.Returns(serviceProvider);

            var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
            serviceScopeFactory.CreateAsyncScope().Returns(serviceScope);
            fixture.Inject(serviceScopeFactory);

            var logger = fixture.Create<ILogger<ServiceBusMessageBatchHandlerBackgroundService<MyMessage>>>();
            fixture.Inject(logger);

            var batchQueueSettings = new ServiceBusMessageBatchHandlerBackgroundService<MyMessage>.BatchQueueSettings(
                "test-queue",
                10,
                TimeSpan.FromSeconds(5));

            fixture.Inject(batchQueueSettings);

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            fixture.Inject(cancellationToken);

            messageHandler.Handle(Arg.Any<IBatchMessageContext<MyMessage>>()).Returns(_ =>
            {
                cancellationTokenSource.Cancel();
                return Task.CompletedTask;
            });
        }
    }

    internal record MyMessage;
}
