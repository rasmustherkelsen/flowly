using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SimpleTransit.AzureServiceBusWrappers;
using SimpleTransit.MessageInfrastructure.BackgroundServices;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.RecurringJobs;
using SimpleTransit.MessageInfrastructure.Senders;
using static SimpleTransit.Test.MessageInfrastructure.BackgroundServices.ServiceBusMessageHandlerBackgroundServiceTest;
using System.Text;
using System.Text.Json;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.BackgroundServices;

public class RecurringJobHandlerBackgroundServiceTest
{
    public class ExecuteAsync
    {
        [Theory, AutoDataWithCustomization(typeof(SetupRecurringJobHandlerBackgroundServiceForTest))]
        internal async Task MustSubmitRecurringJobCreatedMessage(
            RecurringJobHandlerBackgroundService<MyRecurringJobHandler> recurringJobHandlerBackgroundService,
            RecurringJobHandlerBackgroundService<MyRecurringJobHandler>.RecurringJobSettings settings,
            IMessageSender messageSender)
        {
            await recurringJobHandlerBackgroundService.StartAsync(CancellationToken.None);

            await messageSender.Received(1).
                Send(
                    Arg.Is<CreateRecurringJobState>(x =>
                        x.JobTypeName == settings.SessionName &&
                        x.Description == settings.JobDescription &&
                        x.Interval == settings.Interval),
                    Arg.Any<CancellationToken>());
        }

        [Theory, AutoDataWithCustomization(typeof(SetupRecurringJobHandlerBackgroundServiceForTest))]
        internal async Task MustRegisterMessageHandlerAndErrorHandler(
            RecurringJobHandlerBackgroundService<MyRecurringJobHandler> recurringJobHandlerBackgroundService,
            IServiceBusSessionProcessor serviceBusSessionProcessor)
        {
            await recurringJobHandlerBackgroundService.StartAsync(CancellationToken.None);

            serviceBusSessionProcessor.Received(1).ProcessMessageAsync += Arg.Any<Func<ProcessSessionMessageEventArgs, Task>>();
            serviceBusSessionProcessor.Received(1).ProcessErrorAsync += Arg.Any<Func<ProcessErrorEventArgs, Task>>();
        }

        [Theory, AutoDataWithCustomization(typeof(SetupRecurringJobHandlerBackgroundServiceForTest))]
        internal async Task MustStartProcessing(
            RecurringJobHandlerBackgroundService<MyRecurringJobHandler> recurringJobHandlerBackgroundService,
            IServiceBusSessionProcessor serviceBusSessionProcessor)
        {
            await recurringJobHandlerBackgroundService.StartAsync(CancellationToken.None);

            await serviceBusSessionProcessor.Received(1).StartProcessingAsync(Arg.Any<CancellationToken>());
        }
    }

    public class HandleMessage
    {
        [Theory, AutoDataWithCustomization(typeof(SetupRecurringJobHandlerBackgroundServiceForTest))]
        internal async Task MustSetJobStateToStarted(
            RecurringJobHandlerBackgroundService<MyRecurringJobHandler> recurringJobHandlerBackgroundService,
            IServiceBusSessionProcessor serviceBusSessionProcessor,
            ProcessSessionMessageEventArgs processSessionMessageEventArgs,
            IMessageSender messageSender)
        {
            await recurringJobHandlerBackgroundService.StartAsync(CancellationToken.None);

            serviceBusSessionProcessor.ProcessMessageAsync += Raise.Event<Func<ProcessSessionMessageEventArgs, Task>>(processSessionMessageEventArgs);

            await messageSender.Received(1).
                Send(
                    Arg.Is<UpdateJobState>(x =>
                        x.JobId == Guid.Parse(processSessionMessageEventArgs.Message.MessageId) &&
                        x.JobState == JobState.Started),
                    Arg.Any<CancellationToken>());
        }

        [Theory, AutoDataWithCustomization(typeof(SetupRecurringJobHandlerBackgroundServiceForTest))]
        internal async Task MustInvokeRecurringJobHandler(
            RecurringJobHandlerBackgroundService<MyRecurringJobHandler> recurringJobHandlerBackgroundService,
            IServiceBusSessionProcessor serviceBusSessionProcessor,
            ProcessSessionMessageEventArgs processSessionMessageEventArgs,
            MyRecurringJobHandler myRecurringJobHandler)
        {
            await recurringJobHandlerBackgroundService.StartAsync(CancellationToken.None);

            serviceBusSessionProcessor.ProcessMessageAsync += Raise.Event<Func<ProcessSessionMessageEventArgs, Task>>(processSessionMessageEventArgs);

            Assert.True(myRecurringJobHandler.WasHandled);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupRecurringJobHandlerBackgroundServiceForTest))]
        internal async Task MustSetJobStateToCompleted(
            RecurringJobHandlerBackgroundService<MyRecurringJobHandler> recurringJobHandlerBackgroundService,
            IServiceBusSessionProcessor serviceBusSessionProcessor,
            ProcessSessionMessageEventArgs processSessionMessageEventArgs,
            IMessageSender messageSender)
        {
            await recurringJobHandlerBackgroundService.StartAsync(CancellationToken.None);

            serviceBusSessionProcessor.ProcessMessageAsync += Raise.Event<Func<ProcessSessionMessageEventArgs, Task>>(processSessionMessageEventArgs);

            await messageSender.Received(1).
                Send(
                    Arg.Is<UpdateJobState>(x =>
                        x.JobId == Guid.Parse(processSessionMessageEventArgs.Message.MessageId) &&
                        x.JobState == JobState.Completed),
                    Arg.Any<CancellationToken>());
        }

        [Theory, AutoDataWithCustomization(typeof(HandlerFails))]
        internal async Task MustThrowJobExceptionWhenHandlerFails(
            RecurringJobHandlerBackgroundService<MyRecurringJobHandler> recurringJobHandlerBackgroundService,
            ProcessSessionMessageEventArgs processSessionMessageEventArgs)
        {
            await recurringJobHandlerBackgroundService.StartAsync(CancellationToken.None);

            var ex = await Assert.ThrowsAsync<JobException>(() => recurringJobHandlerBackgroundService.HandleMessage(processSessionMessageEventArgs));

            Assert.Equal("Kaboom", ex.InnerException!.Message);
        }
    }

    public class HandleError
    {
        [Theory, AutoDataWithCustomization(typeof(SetupRecurringJobHandlerBackgroundServiceForTest))]
        internal async Task MustSendJobFailedMessage(
            RecurringJobHandlerBackgroundService<MyRecurringJobHandler> recurringJobHandlerBackgroundService, 
            ProcessErrorEventArgs processErrorEventArgs,
            IMessageSender messageSender,
            JobException jobException)
        {
            await recurringJobHandlerBackgroundService.HandleError(processErrorEventArgs);

            await messageSender.Received(1).Send(Arg.Is<JobFailed>(x => 
                x.JobId == jobException.JobId &&
                x.FaultReason == jobException.InnerException!.Message &&
                x.TimeStamp.Date == DateTime.UtcNow.Date));
        }
    }

    internal class MyRecurringJobHandler(bool fails) : IRecurringJobHandler
    {
        public bool WasHandled { get; set; }

        public Task Handle(CancellationToken cancellationToken)
        {
            WasHandled = true;

            if (fails)
                throw new InvalidOperationException("Kaboom");

            return Task.CompletedTask;
        }
    }

    private class HandlerFails() : SetupRecurringJobHandlerBackgroundServiceForTestBase(true);

    private class SetupRecurringJobHandlerBackgroundServiceForTest : SetupRecurringJobHandlerBackgroundServiceForTestBase;

    private abstract class SetupRecurringJobHandlerBackgroundServiceForTestBase(bool handlerFails = false) : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize(new AutoNSubstituteCustomization());

            var serviceBusSessionProcessor = fixture.Create<IServiceBusSessionProcessor>();
            fixture.Inject(serviceBusSessionProcessor);

            serviceBusSessionProcessor.StartProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

            var serviceBusClient = fixture.Create<IServiceBusClient>();
            fixture.Inject(serviceBusClient);

            serviceBusClient.CreateSessionProcessor(Arg.Any<string>(), Arg.Any<ServiceBusSessionProcessorOptions>()).Returns(serviceBusSessionProcessor);

            var messageSender = fixture.Create<IMessageSender>();
            fixture.Inject(messageSender);

            var myRecurringJobHandler = new MyRecurringJobHandler(handlerFails);
            fixture.Inject(myRecurringJobHandler);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(messageSender);
            serviceCollection.AddSingleton(myRecurringJobHandler);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            fixture.Inject(serviceProvider.GetRequiredService<IServiceScopeFactory>());

            fixture.Inject(fixture.Create<RecurringJobHandlerBackgroundService<MyRecurringJobHandler>.RecurringJobSettings>());

            var processMessageEventArgs = CreateProcessSessionMessageEventArgs(CreateServiceBusReceivedMessage(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new MyMessage()))));
            fixture.Inject(processMessageEventArgs);

            var jobException = new JobException(Guid.NewGuid(), new InvalidOperationException("Kaboom"));
            fixture.Inject(jobException);

            var processErrorEventArgs = new ProcessErrorEventArgs(jobException, ServiceBusErrorSource.Abandon, "Namespace", "Path", "Identifier", CancellationToken.None);
            fixture.Inject(processErrorEventArgs);
        }
    }

    private static ServiceBusReceivedMessage CreateServiceBusReceivedMessage(byte[] body)
    {
        var amqpMessageBody = new AmqpMessageBody([new ReadOnlyMemory<byte>(body)]);

        var amqpAnnotatedMessage = new AmqpAnnotatedMessage(amqpMessageBody);

        amqpAnnotatedMessage.Properties.MessageId = new AmqpMessageId(Guid.NewGuid().ToString());

        return ServiceBusReceivedMessage.FromAmqpMessage(amqpAnnotatedMessage, new BinaryData(new ReadOnlyMemory<byte>(Guid.NewGuid().ToByteArray())));
    }

    private static ProcessSessionMessageEventArgs CreateProcessSessionMessageEventArgs(ServiceBusReceivedMessage receivedMessage)
        => new(receivedMessage, null, CancellationToken.None);
}