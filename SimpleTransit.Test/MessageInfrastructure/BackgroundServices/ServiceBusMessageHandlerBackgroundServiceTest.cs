using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SimpleTransit.AzureServiceBusWrappers;
using SimpleTransit.MessageInfrastructure.BackgroundServices;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.BackgroundServices;

public class ServiceBusMessageHandlerBackgroundServiceTest
{
    public class ExecuteAsync
    {
        [Theory, AutoDataWithCustomization(typeof(SetupServiceBusMessageHandlerBackgroundServiceForTest))]
        internal async Task MustRegisterMessageProcessorAndErrorHandler(ServiceBusMessageHandlerBackgroundService<MyMessage> serviceBusMessageHandlerBackgroundService, IServiceBusProcessor serviceBusProcessor)
        {
            await serviceBusMessageHandlerBackgroundService.StartAsync(CancellationToken.None);

            serviceBusProcessor.Received(1).ProcessMessageAsync += Arg.Any<Func<ProcessMessageEventArgs, Task>>();
            serviceBusProcessor.Received(1).ProcessErrorAsync += Arg.Any<Func<ProcessErrorEventArgs, Task>>();
        }

        [Theory, AutoDataWithCustomization(typeof(SetupServiceBusMessageHandlerBackgroundServiceForTest))]
        internal async Task MustStartProcessing(ServiceBusMessageHandlerBackgroundService<MyMessage> serviceBusMessageHandlerBackgroundService, IServiceBusProcessor serviceBusProcessor)
        {
            await serviceBusMessageHandlerBackgroundService.StartAsync(CancellationToken.None);

            await serviceBusProcessor.Received(1).StartProcessingAsync(Arg.Any<CancellationToken>());
        }
    }

    public class HandleMessage
    {
        [Theory, AutoDataWithCustomization(typeof(SetupServiceBusMessageHandlerBackgroundServiceForTest))]
        internal async Task MustInvokeMessageHandlerWhenProcessorReceivesNewMessage(
            ServiceBusMessageHandlerBackgroundService<MyMessage> serviceBusMessageHandlerBackgroundService, 
            IServiceBusProcessor serviceBusProcessor,
            ProcessMessageEventArgs processMessageEventArgs,
            IMessageHandler<MyMessage> messageHandler)
        {
            await serviceBusMessageHandlerBackgroundService.StartAsync(CancellationToken.None);

            serviceBusProcessor.ProcessMessageAsync += Raise.Event<Func<ProcessMessageEventArgs, Task>>(processMessageEventArgs);

            await messageHandler.Received(1).Handle(Arg.Is<IMessageContext<MyMessage>>(x => x != null));
        }
    }

    public class StopAsync
    {
        [Theory, AutoDataWithCustomization(typeof(SetupServiceBusMessageHandlerBackgroundServiceForTest))]
        internal async Task MustStopAndDisposeProcessor(ServiceBusMessageHandlerBackgroundService<MyMessage> serviceBusMessageHandlerBackgroundService, IServiceBusProcessor serviceBusProcessor)
        {
            await serviceBusMessageHandlerBackgroundService.StopAsync(CancellationToken.None);

            await serviceBusProcessor.Received(1).StopProcessingAsync(Arg.Any<CancellationToken>());
            await serviceBusProcessor.Received(1).DisposeAsync();
        }
    }

    internal class MyMessage;

    private class SetupServiceBusMessageHandlerBackgroundServiceForTest : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize(new AutoNSubstituteCustomization());

            var serviceBusProcessor = fixture.Create<IServiceBusProcessor>();
            fixture.Inject(serviceBusProcessor);

            serviceBusProcessor.StartProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

            var serviceBusClient = fixture.Create<IServiceBusClient>();
            fixture.Inject(serviceBusClient);

            serviceBusClient.CreateProcessor(Arg.Any<string>(), Arg.Any<ServiceBusProcessorOptions>()).Returns(serviceBusProcessor);

            var processMessageEventArgs = MessageBusHelper.CreateProcessMessageEventArgs(new MyMessage());
            fixture.Inject(processMessageEventArgs);

            var serviceCollection = new ServiceCollection();

            var messageHandler = fixture.Create<IMessageHandler<MyMessage>>();
            fixture.Inject(messageHandler);

            serviceCollection.AddSingleton(messageHandler);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            fixture.Inject(serviceProvider.GetRequiredService<IServiceScopeFactory>());
        }
    }
}
