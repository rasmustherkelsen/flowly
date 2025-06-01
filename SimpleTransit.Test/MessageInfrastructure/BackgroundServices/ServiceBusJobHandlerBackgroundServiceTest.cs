using AutoFixture.AutoNSubstitute;
using AutoFixture;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SimpleTransit.AzureServiceBusWrappers;
using SimpleTransit.MessageInfrastructure.Receivers;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.BackgroundServices;
using SimpleTransit.MessageInfrastructure.Senders;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.BackgroundServices;

public class ServiceBusJobHandlerBackgroundServiceTest
{
    public class OnHandleMessage
    {
        [Theory, AutoDataWithCustomization(typeof(SetupServiceBusJobHandlerBackgroundServiceForTest))]
        internal async Task MustSendJobStartedMessage(
            ServiceBusJobHandlerBackgroundService<MyJobMessage> serviceBusJobHandlerBackgroundService,
            IServiceBusProcessor serviceBusProcessor,
            ProcessMessageEventArgs processMessageEventArgs,
            IMessageSender messageSender)
        {
            await serviceBusJobHandlerBackgroundService.StartAsync(CancellationToken.None);

            serviceBusProcessor.ProcessMessageAsync += Raise.Event<Func<ProcessMessageEventArgs, Task>>(processMessageEventArgs);

            await messageSender.Received(1).Send(Arg.Is<UpdateJobState>(x => x != null && x.JobId == Guid.Parse(processMessageEventArgs.Message.MessageId) && x.JobState == JobState.Started));
        }

        [Theory, AutoDataWithCustomization(typeof(SetupServiceBusJobHandlerBackgroundServiceForTest))]
        internal async Task MustSendJobCompletedMessage(
            ServiceBusJobHandlerBackgroundService<MyJobMessage> serviceBusJobHandlerBackgroundService,
            IServiceBusProcessor serviceBusProcessor,
            ProcessMessageEventArgs processMessageEventArgs,
            IMessageSender messageSender)
        {
            await serviceBusJobHandlerBackgroundService.StartAsync(CancellationToken.None);

            serviceBusProcessor.ProcessMessageAsync += Raise.Event<Func<ProcessMessageEventArgs, Task>>(processMessageEventArgs);

            await messageSender.Received(1).Send(Arg.Is<UpdateJobState>(x => x != null && x.JobId == Guid.Parse(processMessageEventArgs.Message.MessageId) && x.JobState == JobState.Completed));
        }

        [Theory, AutoDataWithCustomization(typeof(SetupServiceBusJobHandlerBackgroundServiceForTest))]
        internal async Task MustDelegateToHandler(
            ServiceBusJobHandlerBackgroundService<MyJobMessage> serviceBusJobHandlerBackgroundService,
            IServiceBusProcessor serviceBusProcessor,
            ProcessMessageEventArgs processMessageEventArgs,
            IJobMessageHandler<MyJobMessage> jobHandler)
        {
            await serviceBusJobHandlerBackgroundService.StartAsync(CancellationToken.None);

            serviceBusProcessor.ProcessMessageAsync += Raise.Event<Func<ProcessMessageEventArgs, Task>>(processMessageEventArgs);

            await jobHandler.Received(1).Handle(Arg.Is<IJobMessageContext<MyJobMessage>>(x => x != null));
        }

        [Theory, AutoDataWithCustomization(typeof(JobHandlerFails))]
        internal async Task MustThrowJobExceptionOnHandlerError(
            ServiceBusJobHandlerBackgroundService<MyJobMessage> serviceBusJobHandlerBackgroundService,
            ProcessMessageEventArgs processMessageEventArgs,
            MyJobMessage message,
            IServiceProvider serviceProvider)
        {
            await serviceBusJobHandlerBackgroundService.StartAsync(CancellationToken.None);

            var ex = await Assert.ThrowsAsync<JobException>(() => serviceBusJobHandlerBackgroundService.OnHandleMessage(message, processMessageEventArgs, serviceProvider));

            Assert.Equal("Handler failed", ex.InnerException!.Message);
        }
    }

    public class OnMessageHandlingError
    {
        [Theory, AutoDataWithCustomization(typeof(SetupServiceBusJobHandlerBackgroundServiceForTest))]
        internal async Task MustSubmitJobFailedMessage(
            ServiceBusJobHandlerBackgroundService<MyJobMessage> serviceBusJobHandlerBackgroundService,
            IServiceBusProcessor serviceBusProcessor,
            ProcessErrorEventArgs processErrorEventArgs,
            IMessageSender messageSender)
        {
            await serviceBusJobHandlerBackgroundService.StartAsync(CancellationToken.None);

            serviceBusProcessor.ProcessErrorAsync += Raise.Event<Func<ProcessErrorEventArgs, Task>>(processErrorEventArgs);

            await messageSender.Received(1).Send(Arg.Is<JobFailed>(x => x != null), Arg.Any<CancellationToken>());
        }
    }

    internal class MyJobMessage : IJobMessage
    {
        public string Description => "Test job description";
        public string JobTypeName => "TestJobType";
    }

    private class JobHandlerFails() : SetupServiceBusJobHandlerBackgroundServiceForTestBase(true);

    private class SetupServiceBusJobHandlerBackgroundServiceForTest : SetupServiceBusJobHandlerBackgroundServiceForTestBase;

    private abstract class SetupServiceBusJobHandlerBackgroundServiceForTestBase(bool handlerFails = false) : ICustomization
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

            var processMessageEventArgs = MessageBusHelper.CreateProcessMessageEventArgs(new MyJobMessage());
            fixture.Inject(processMessageEventArgs);

            var serviceCollection = new ServiceCollection();

            var messageHandler = fixture.Create<IJobMessageHandler<MyJobMessage>>();
            fixture.Inject(messageHandler);

            if (handlerFails)
            {
                messageHandler.Handle(Arg.Any<IJobMessageContext<MyJobMessage>>()).Returns(Task.FromException(new Exception("Handler failed")));
            }

            serviceCollection.AddSingleton(messageHandler);

            var messageSender = fixture.Create<IMessageSender>();
            fixture.Inject(messageSender);
            serviceCollection.AddSingleton(messageSender);
            
            IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            fixture.Inject(serviceProvider);
            fixture.Inject(serviceProvider.GetRequiredService<IServiceScopeFactory>());

            var processErrorEventArgs = new ProcessErrorEventArgs(
                new JobException(Guid.NewGuid(), new Exception("Inner Exception Message")),
                ServiceBusErrorSource.Abandon,
                null,
                null,
                null,
                CancellationToken.None);

            fixture.Inject(processErrorEventArgs);
        }
    }
}