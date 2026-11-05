using System.Diagnostics;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.SendSteps;
using Flowly.MessageInfrastructure.Senders;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.Senders;

public class MessageSubmitterTests
{
    public class Submit
    {
        [Fact]
        public async Task CreatesSenderForConfiguredQueueName()
        {
            var capturingSender = new CapturingSender();
            var client = new FakeMessageBusClient(capturingSender);
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubSubmitterInstrumentation();
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, "order-placed", "primary", instrumentation, SendStepRunner.None);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            Assert.Equal("order-placed", client.CreatedSenderForQueue);
        }

        [Fact]
        public async Task SendsMessageWithGeneratedMessageIdAndEmptyCorrelationId()
        {
            var capturingSender = new CapturingSender();
            var client = new FakeMessageBusClient(capturingSender);
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubSubmitterInstrumentation();
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, "order-placed", "primary", instrumentation, SendStepRunner.None);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            var properties = capturingSender.SentMessages[0].Properties;
            Assert.True(Guid.TryParse(properties.MessageId, out _));
            Assert.Equal(string.Empty, properties.CorrelationId);
        }

        [Fact]
        public async Task SuccessfulSubmit_RecordsSentOnInstrumentation()
        {
            var client = new FakeMessageBusClient(new CapturingSender());
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubSubmitterInstrumentation();
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, "order-placed", "primary", instrumentation, SendStepRunner.None);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            Assert.Single(instrumentation.Sent);
            Assert.Equal("order-placed", instrumentation.Sent[0].QueueName);
            Assert.Empty(instrumentation.Failed);
        }

        [Fact]
        public async Task WhenSenderThrows_RecordsFailedAndRethrows()
        {
            var client = new FakeMessageBusClient(new ThrowingSender(new InvalidOperationException("boom")));
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubSubmitterInstrumentation();
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, "order-placed", "primary", instrumentation, SendStepRunner.None);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None));

            Assert.Equal("boom", exception.Message);
            Assert.Single(instrumentation.Failed);
            Assert.Equal("order-placed", instrumentation.Failed[0]);
            Assert.Empty(instrumentation.Sent);
        }

        [Fact]
        public async Task StartsSendingActivityWithMessagingSystemAndMessageId()
        {
            var capturingSender = new CapturingSender();
            var client = new FakeMessageBusClient(capturingSender) { MessagingSystem = "fake-bus" };
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubSubmitterInstrumentation();
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, "order-placed", "primary", instrumentation, SendStepRunner.None);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            var sentMessageId = capturingSender.SentMessages[0].Properties.MessageId;
            Assert.Single(instrumentation.Started);
            Assert.Equal("order-placed", instrumentation.Started[0].QueueName);
            Assert.Equal("fake-bus", instrumentation.Started[0].MessagingSystem);
            Assert.Equal(sentMessageId, instrumentation.Started[0].MessageId);
        }

        [Fact]
        public async Task PassesCancellationTokenThroughToSender()
        {
            var capturingSender = new CapturingSender();
            var client = new FakeMessageBusClient(capturingSender);
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubSubmitterInstrumentation();
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, "order-placed", "primary", instrumentation, SendStepRunner.None);
            using var cancellationTokenSource = new CancellationTokenSource();

            await messageSubmitter.Submit(new OrderPlaced("x"), cancellationTokenSource.Token);

            Assert.Equal(cancellationTokenSource.Token, capturingSender.ReceivedTokens.Single());
        }

        [Fact]
        public async Task PassesPartitionKeyThroughToSentMessageProperties()
        {
            var capturingSender = new CapturingSender();
            var client = new FakeMessageBusClient(capturingSender);
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubSubmitterInstrumentation();
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, "order-placed", "primary", instrumentation, SendStepRunner.None);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None, partitionKey: "customer-7");

            Assert.Equal("customer-7", capturingSender.SentMessages[0].Properties.PartitionKey);
        }

        [Fact]
        public async Task OnStreamOnlyClient_SendsThroughStreamSender()
        {
            var sender = new FakeMessageBusSender();
            var client = new FakeStreamOnlyClient(sender);
            var messageSubmitter = CreateMessageSubmitter(client, isStream: true);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            Assert.Equal("order-placed", client.CreatedStreamSenderForQueue);
            Assert.Single(sender.SentMessages);
        }

        [Fact]
        public async Task OnStreamOnlyClient_PassesPartitionKeyOnMessageProperties()
        {
            var sender = new FakeMessageBusSender();
            var messageSubmitter = CreateMessageSubmitter(new FakeStreamOnlyClient(sender), isStream: true);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None, "customer-1");

            Assert.Equal("customer-1", sender.SentMessages[0].Properties.PartitionKey);
        }

        [Fact]
        public async Task OnStreamOnlyClient_TagsInstrumentationWithClientMessagingSystem()
        {
            var instrumentation = new StubSubmitterInstrumentation();
            var messageSubmitter = CreateMessageSubmitter(new FakeStreamOnlyClient(new FakeMessageBusSender()), isStream: true, instrumentation);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            Assert.Equal("fake-stream-only", instrumentation.Started.Single().MessagingSystem);
        }

        [Fact]
        public async Task OnQueueAndStreamClientWithoutSenderOverride_FallsBackToMessageBusSender()
        {
            var sender = new FakeMessageBusSender();
            var client = new FakeQueueAndStreamClient(sender);
            var messageSubmitter = CreateMessageSubmitter(client, isStream: true);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            Assert.Equal("order-placed", client.CreatedSenderForQueue);
            Assert.Single(sender.SentMessages);
        }

        [Fact]
        public async Task QueueMessageOnStreamOnlyClient_ThrowsDescriptiveErrorAndRecordsFailure()
        {
            var instrumentation = new StubSubmitterInstrumentation();
            var messageSubmitter = CreateMessageSubmitter(new FakeStreamOnlyClient(new FakeMessageBusSender()), isStream: false, instrumentation);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None));

            Assert.Contains("'primary' does not support queue-based messaging", exception.Message);
            Assert.Single(instrumentation.Failed);
        }

        private static MessageSubmitter<OrderPlaced> CreateMessageSubmitter(
            ITransportClient client,
            bool isStream,
            StubSubmitterInstrumentation? instrumentation = null)
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("primary", client, null);

            return new MessageSubmitter<OrderPlaced>(
                registry,
                "order-placed",
                "primary",
                instrumentation ?? new StubSubmitterInstrumentation(),
                SendStepRunner.None,
                isStream);
        }
    }

    public class WhenMessageImplementsIOpenTelemetryTagsProvider
    {
        [Fact]
        public async Task SetsTagsOnSpan()
        {
            var instrumentation = new ActivityReturningStubInstrumentation();
            var client = new FakeMessageBusClient(new CapturingSender());
            var registry = new SingleClientRegistry(client, "primary");
            var messageSubmitter = new MessageSubmitter<TaggedOrder>(registry, "orders", "primary", instrumentation, SendStepRunner.None);

            await messageSubmitter.Submit(new TaggedOrder("order-123"), CancellationToken.None);

            Assert.Equal("order-123", instrumentation.Activity.GetTagItem("order.id"));
        }

        [Fact]
        public async Task WhenMessageDoesNotImplementInterface_SetsNoCustomTags()
        {
            var instrumentation = new ActivityReturningStubInstrumentation();
            var client = new FakeMessageBusClient(new CapturingSender());
            var registry = new SingleClientRegistry(client, "primary");
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, "order-placed", "primary", instrumentation, SendStepRunner.None);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            Assert.Null(instrumentation.Activity.GetTagItem("order.id"));
        }
    }

    public class WithSendSteps
    {
        [Fact]
        public async Task StepRunsBeforeAnythingIsSent()
        {
            var observations = new List<string>();
            var capturingSender = new CapturingSender();
            var instrumentation = new StubSubmitterInstrumentation();
            var services = new ServiceCollection();
            services.AddSingleton(observations);
            services.AddSingleton<Func<string>>(() => $"sent:{capturingSender.SentMessages.Count},started:{instrumentation.Started.Count}");
            services.AddScoped<ObservingStep>();
            using var serviceProvider = services.BuildServiceProvider();
            var messageSubmitter = CreateMessageSubmitter(serviceProvider, capturingSender, instrumentation, typeof(ObservingStep));

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            Assert.Equal(["before:sent:0,started:0", "after:sent:1,started:1"], observations);
        }

        [Fact]
        public async Task StepThatThrows_SendsNothingAndRecordsNoMetrics()
        {
            var capturingSender = new CapturingSender();
            var instrumentation = new StubSubmitterInstrumentation();
            using var serviceProvider = CreateServiceProvider([], typeof(RejectingStep));
            var messageSubmitter = CreateMessageSubmitter(serviceProvider, capturingSender, instrumentation, typeof(RejectingStep));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None));

            Assert.Empty(capturingSender.SentMessages);
            Assert.Empty(instrumentation.Started);
            Assert.Empty(instrumentation.Sent);
            Assert.Empty(instrumentation.Failed);
        }

        [Fact]
        public async Task StepThatThrows_ExceptionReachesCallerUnchanged()
        {
            using var serviceProvider = CreateServiceProvider([], typeof(RejectingStep));
            var messageSubmitter = CreateMessageSubmitter(serviceProvider, new CapturingSender(), new StubSubmitterInstrumentation(), typeof(RejectingStep));

            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None));

            Assert.Equal("rejected", exception.Message);
        }

        [Fact]
        public async Task StepThatSkipsNext_SendsNothingAndDoesNotThrow()
        {
            var capturingSender = new CapturingSender();
            var instrumentation = new StubSubmitterInstrumentation();
            using var serviceProvider = CreateServiceProvider([], typeof(SkippingStep));
            var messageSubmitter = CreateMessageSubmitter(serviceProvider, capturingSender, instrumentation, typeof(SkippingStep));

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            Assert.Empty(capturingSender.SentMessages);
            Assert.Empty(instrumentation.Sent);
            Assert.Empty(instrumentation.Failed);
        }

        [Fact]
        public async Task StepPassingThrough_SendsMessageAndRecordsSent()
        {
            var capturingSender = new CapturingSender();
            var instrumentation = new StubSubmitterInstrumentation();
            using var serviceProvider = CreateServiceProvider([], typeof(PassThroughStep));
            var messageSubmitter = CreateMessageSubmitter(serviceProvider, capturingSender, instrumentation, typeof(PassThroughStep));
            var message = new OrderPlaced("x");

            await messageSubmitter.Submit(message, CancellationToken.None);

            Assert.Same(message, capturingSender.SentMessages.Single().Message);
            Assert.Single(instrumentation.Sent);
        }

        [Fact]
        public async Task OnStream_PreservesPartitionKeyWhenStepCallsNext()
        {
            var sender = new FakeMessageBusSender();
            using var serviceProvider = CreateServiceProvider([], typeof(PassThroughStep));
            var registry = new MessageBusClientRegistry();
            registry.Register("primary", new FakeStreamOnlyClient(sender), null);
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(
                registry,
                "order-placed",
                "primary",
                new StubSubmitterInstrumentation(),
                CreateSendStepRunner(serviceProvider, typeof(PassThroughStep)),
                isStream: true);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None, "customer-9");

            Assert.Equal("customer-9", sender.SentMessages.Single().Properties.PartitionKey);
        }

        [Fact]
        public async Task OnStream_StepThatThrows_AppendsNothing()
        {
            var sender = new FakeMessageBusSender();
            using var serviceProvider = CreateServiceProvider([], typeof(RejectingStep));
            var registry = new MessageBusClientRegistry();
            registry.Register("primary", new FakeStreamOnlyClient(sender), null);
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(
                registry,
                "order-placed",
                "primary",
                new StubSubmitterInstrumentation(),
                CreateSendStepRunner(serviceProvider, typeof(RejectingStep)),
                isStream: true);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None, "customer-9"));

            Assert.Empty(sender.SentMessages);
        }

        [Fact]
        public async Task FlowlySystemMessage_BypassesSteps()
        {
            var capturingSender = new CapturingSender();
            var observations = new List<string>();
            using var serviceProvider = CreateServiceProvider(observations, typeof(RejectingStep));
            var messageSubmitter = new MessageSubmitter<InternalMessage>(
                new SingleClientRegistry(new FakeMessageBusClient(capturingSender), "primary"),
                "internal",
                "primary",
                new StubSubmitterInstrumentation(),
                CreateSendStepRunner(serviceProvider, typeof(RejectingStep)));

            await messageSubmitter.Submit(new InternalMessage(), CancellationToken.None);

            Assert.Single(capturingSender.SentMessages);
        }

        [Fact]
        public async Task WithoutRegisteredSteps_CreatesNoScope()
        {
            var capturingSender = new CapturingSender();
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(
                new SingleClientRegistry(new FakeMessageBusClient(capturingSender), "primary"),
                "order-placed",
                "primary",
                new StubSubmitterInstrumentation(),
                new SendStepRunner([], new ThrowingServiceScopeFactory()));

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            Assert.Single(capturingSender.SentMessages);
        }

        [Fact]
        public async Task StepsGetAScopeOfTheirOwnInsteadOfTheCallersScope()
        {
            var observations = new List<string>();
            using var serviceProvider = CreateServiceProvider(observations, typeof(ScopeIdentityStep));
            using var callerScope = serviceProvider.CreateScope();
            var messageSubmitter = CreateMessageSubmitter(serviceProvider, new CapturingSender(), new StubSubmitterInstrumentation(), typeof(ScopeIdentityStep));

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);
            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            var callerIdentity = $"scoped:{callerScope.ServiceProvider.GetRequiredService<ScopedDependency>().GetHashCode()}";
            Assert.Equal(2, observations.Distinct().Count());
            Assert.DoesNotContain(callerIdentity, observations);
        }

        [Fact]
        public async Task PassesMessageAndCancellationTokenToTheStep()
        {
            var contexts = new List<SendStepContext<OrderPlaced>>();
            var services = new ServiceCollection();
            services.AddSingleton(contexts);
            services.AddScoped<ContextCapturingStep>();
            using var serviceProvider = services.BuildServiceProvider();
            var messageSubmitter = CreateMessageSubmitter(serviceProvider, new CapturingSender(), new StubSubmitterInstrumentation(), typeof(ContextCapturingStep));
            var message = new OrderPlaced("x");
            using var cancellationTokenSource = new CancellationTokenSource();

            await messageSubmitter.Submit(message, cancellationTokenSource.Token);

            var sendStepContext = Assert.Single(contexts);
            Assert.Same(message, sendStepContext.Message);
            Assert.Equal(cancellationTokenSource.Token, sendStepContext.CancellationToken);
        }

        private static ServiceProvider CreateServiceProvider(List<string> observations, params Type[] stepTypes)
        {
            var services = new ServiceCollection();
            services.AddSingleton(observations);
            services.AddScoped<ScopedDependency>();

            foreach (var stepType in stepTypes)
            {
                services.AddScoped(stepType);
            }

            return services.BuildServiceProvider();
        }

        private static SendStepRunner CreateSendStepRunner(IServiceProvider serviceProvider, params Type[] stepTypes)
            => new(
                stepTypes.Select(stepType => new SendStepRegistration(stepType, SendStepRegistration.ResolveFilterType(stepType))),
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

        private static MessageSubmitter<OrderPlaced> CreateMessageSubmitter(
            IServiceProvider serviceProvider,
            CapturingSender capturingSender,
            StubSubmitterInstrumentation instrumentation,
            params Type[] stepTypes)
            => new(
                new SingleClientRegistry(new FakeMessageBusClient(capturingSender), "primary"),
                "order-placed",
                "primary",
                instrumentation,
                CreateSendStepRunner(serviceProvider, stepTypes));
    }

    private record OrderPlaced(string OrderId);

    private record InternalMessage : IFlowlySystemMessage;

    private sealed class ScopedDependency;

    private sealed class ThrowingServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("no scope expected");
    }

    private sealed class ObservingStep(List<string> observations, Func<string> probe) : SendStep
    {
        public override async Task Handle(SendStepContext<object> context, SendStepDelegate next)
        {
            observations.Add($"before:{probe()}");
            await next();
            observations.Add($"after:{probe()}");
        }
    }

    private sealed class RejectingStep : SendStep
    {
        public override Task Handle(SendStepContext<object> context, SendStepDelegate next)
            => throw new UnauthorizedAccessException("rejected");
    }

    private sealed class SkippingStep : SendStep
    {
        public override Task Handle(SendStepContext<object> context, SendStepDelegate next) => Task.CompletedTask;
    }

    private sealed class PassThroughStep : SendStep;

    private sealed class ScopeIdentityStep(List<string> observations, ScopedDependency scopedDependency) : SendStep
    {
        public override Task Handle(SendStepContext<object> context, SendStepDelegate next)
        {
            observations.Add($"scoped:{scopedDependency.GetHashCode()}");

            return next();
        }
    }

    private sealed class ContextCapturingStep(List<SendStepContext<OrderPlaced>> contexts) : SendStep<OrderPlaced>
    {
        public override Task Handle(SendStepContext<OrderPlaced> context, SendStepDelegate next)
        {
            contexts.Add(context);

            return next();
        }
    }

    private record TaggedOrder(string OrderId) : IOpenTelemetryTagsProvider
    {
        public IEnumerable<KeyValuePair<string, object?>> GetOpenTelemetryTags() =>
            [new("order.id", OrderId)];
    }

    private sealed class SingleClientRegistry(IMessageBusClient client, string primary) : IMessageBusClientRegistry
    {
        public string PrimaryProviderName { get; } = primary;

        public IMessageBusClient GetClient(string providerName)
        {
            return client;
        }

        public bool IsRegistered(string providerName)
        {
            return true;
        }

        public IReadOnlyList<RegisteredTransport> GetAll()
        {
            return [new RegisteredTransport(PrimaryProviderName, true, null)];
        }

        public void Register(string providerName, IMessageBusClient client, bool? createTopologyOverride)
        {
        }
    }

    private sealed class FakeMessageBusClient(IMessageBusSender sender) : IMessageBusClient
    {
        public string? CreatedSenderForQueue { get; private set; }
        public string MessagingSystem { get; set; } = "fake";

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
        {
            CreatedSenderForQueue = queueName;
            return Task.FromResult(sender);
        }

        public Task<IMessageBusReceiver> CreateReceiver(string queueName)
        {
            throw new NotSupportedException();
        }

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
        {
            throw new NotSupportedException();
        }

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
        {
            throw new NotSupportedException();
        }

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName)
        {
            throw new NotSupportedException();
        }

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class CapturingSender : IMessageBusSender
    {
        public List<(object Message, MessageProperties Properties)> SentMessages { get; } = [];
        public List<CancellationToken> ReceivedTokens { get; } = [];

        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            SentMessages.Add((message!, messageProperties));
            ReceivedTokens.Add(cancellationToken);
            return Task.CompletedTask;
        }

        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSender(Exception exception) : IMessageBusSender
    {
        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            return Task.FromException(exception);
        }

        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ActivityReturningStubInstrumentation : ISubmitterInstrumentation
    {
        public Activity Activity { get; } = new Activity("flowly.send test").Start();

        public bool IsEnabled => true;

        public Activity? StartSending(string queueName, string messagingSystem, string messageId) => Activity;

        public void RecordSent(string queueName, double durationMs) { }

        public void RecordFailed(string queueName) { }
    }

    private sealed class StubSubmitterInstrumentation : ISubmitterInstrumentation
    {
        public List<(string QueueName, string MessagingSystem, string MessageId)> Started { get; } = [];
        public List<(string QueueName, double DurationMs)> Sent { get; } = [];
        public List<string> Failed { get; } = [];

        public bool IsEnabled => true;

        public Activity? StartSending(string queueName, string messagingSystem, string messageId)
        {
            Started.Add((queueName, messagingSystem, messageId));
            return null;
        }

        public void RecordSent(string queueName, double durationMs)
        {
            Sent.Add((queueName, durationMs));
        }

        public void RecordFailed(string queueName)
        {
            Failed.Add(queueName);
        }
    }
}