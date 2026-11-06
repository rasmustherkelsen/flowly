using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Senders;
using Flowly.Jobs.Tests.Fakes;
using Flowly.MessageInfrastructure.Senders;
using Flowly.MessageInfrastructure.SendSteps;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Tests.Senders;

public class JobSubmitterTests
{
    public class SubmitJob
    {
        [Fact]
        public async Task ReturnsNewlyGeneratedJobIdWithNonEmptyGuid()
        {
            var fakeMessageSender = new FakeMessageSender();
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var jobSubmitter = new JobSubmitter<SomeJobMessage>(fakeMessageBusClientRegistry, "some-job-queue", "primary", fakeMessageSender, SendStepRunner.None);

            var jobId = await jobSubmitter.SubmitJob(new SomeJobMessage());

            Assert.NotEqual(Guid.Empty, jobId.InnerId);
        }

        [Fact]
        public async Task SendsCreateJobStateMessageWithJobTypeNameAndDescription()
        {
            var fakeMessageSender = new FakeMessageSender();
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var jobSubmitter = new JobSubmitter<SomeJobMessage>(fakeMessageBusClientRegistry, "some-job-queue", "primary", fakeMessageSender, SendStepRunner.None);

            await jobSubmitter.SubmitJob(new SomeJobMessage());

            var createJobState = Assert.IsType<FlowlysysCreateJobStateMessage>(Assert.Single(fakeMessageSender.SentMessages));
            Assert.Equal("SomeJob", createJobState.JobTypeName);
            Assert.Equal("some description", createJobState.Description);
        }

        [Fact]
        public async Task SendsMessageToConfiguredQueueOnConfiguredProviderClient()
        {
            var fakeMessageSender = new FakeMessageSender();
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var jobSubmitter = new JobSubmitter<SomeJobMessage>(fakeMessageBusClientRegistry, "some-job-queue", "primary", fakeMessageSender, SendStepRunner.None);
            var message = new SomeJobMessage();

            await jobSubmitter.SubmitJob(message);

            var sender = fakeMessageBusClient.GetSender("some-job-queue");
            var (sentMessage, _) = Assert.Single(sender.SentMessages);
            Assert.Same(message, sentMessage);
        }

        [Fact]
        public async Task MessagePropertiesContainJobIdAsMessageId()
        {
            var fakeMessageSender = new FakeMessageSender();
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var jobSubmitter = new JobSubmitter<SomeJobMessage>(fakeMessageBusClientRegistry, "some-job-queue", "primary", fakeMessageSender, SendStepRunner.None);

            var jobId = await jobSubmitter.SubmitJob(new SomeJobMessage());

            var sender = fakeMessageBusClient.GetSender("some-job-queue");
            var (_, properties) = sender.SentMessages[0];
            Assert.Equal(jobId.InnerId.ToString(), properties.MessageId);
        }

        [Fact]
        public async Task SendsJobMessageWithCustomProperties()
        {
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var jobSubmitter = new JobSubmitter<SomeJobMessage>(fakeMessageBusClientRegistry, "some-job-queue", "primary", new FakeMessageSender(), SendStepRunner.None);

            await jobSubmitter.SubmitJob(new SomeJobMessage(), customProperties: new Dictionary<string, string> { ["tenant"] = "contoso" });

            var (_, properties) = fakeMessageBusClient.GetSender("some-job-queue").SentMessages[0];
            Assert.Equal("contoso", properties.CustomProperties["tenant"]);
        }

        [Fact]
        public async Task WithReservedCustomPropertyKey_ThrowsArgumentExceptionBeforeCreatingJobState()
        {
            var fakeMessageSender = new FakeMessageSender();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(new FakeMessageBusClient());
            var jobSubmitter = new JobSubmitter<SomeJobMessage>(fakeMessageBusClientRegistry, "some-job-queue", "primary", fakeMessageSender, SendStepRunner.None);

            await Assert.ThrowsAsync<ArgumentException>(() => jobSubmitter.SubmitJob(
                new SomeJobMessage(),
                customProperties: new Dictionary<string, string> { ["flowly-x"] = "1" }));

            Assert.Empty(fakeMessageSender.SentMessages);
        }

        [Fact]
        public async Task CreateJobStateCarriesSameJobIdAsSubmittedMessage()
        {
            var fakeMessageSender = new FakeMessageSender();
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var jobSubmitter = new JobSubmitter<SomeJobMessage>(fakeMessageBusClientRegistry, "some-job-queue", "primary", fakeMessageSender, SendStepRunner.None);

            var jobId = await jobSubmitter.SubmitJob(new SomeJobMessage());

            var createJobState = (FlowlysysCreateJobStateMessage)fakeMessageSender.SentMessages[0];
            Assert.Equal(jobId, createJobState.JobId);
        }
    }

    public class WithSendSteps
    {
        [Fact]
        public async Task StepRunsBeforeCreateJobStateMessageAndJobMessageAreSent()
        {
            var observations = new List<string>();
            var fakeMessageSender = new FakeMessageSender();
            var fakeMessageBusClient = new FakeMessageBusClient();
            var services = new ServiceCollection();
            services.AddSingleton(observations);
            services.AddSingleton<Func<string>>(() => $"state:{fakeMessageSender.SentMessages.Count},job:{CountJobMessages(fakeMessageBusClient)}");
            services.AddScoped<ObservingStep>();
            using var serviceProvider = services.BuildServiceProvider();
            var jobSubmitter = CreateSubmitter(serviceProvider, fakeMessageSender, fakeMessageBusClient, typeof(ObservingStep));

            await jobSubmitter.SubmitJob(new SomeJobMessage());

            Assert.Equal(["before:state:0,job:0", "after:state:1,job:1"], observations);
        }

        [Fact]
        public async Task StepThatThrows_SendsNeitherCreateJobStateMessageNorJobMessage()
        {
            var fakeMessageSender = new FakeMessageSender();
            var fakeMessageBusClient = new FakeMessageBusClient();
            using var serviceProvider = CreateServiceProvider(typeof(RejectingStep));
            var jobSubmitter = CreateSubmitter(serviceProvider, fakeMessageSender, fakeMessageBusClient, typeof(RejectingStep));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => jobSubmitter.SubmitJob(new SomeJobMessage()));

            Assert.Empty(fakeMessageSender.SentMessages);
            Assert.Empty(fakeMessageBusClient.CreatedSenders);
        }

        [Fact]
        public async Task StepThatThrows_ExceptionReachesCallerUnchanged()
        {
            using var serviceProvider = CreateServiceProvider(typeof(RejectingStep));
            var jobSubmitter = CreateSubmitter(serviceProvider, new FakeMessageSender(), new FakeMessageBusClient(), typeof(RejectingStep));

            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => jobSubmitter.SubmitJob(new SomeJobMessage()));

            Assert.Equal("rejected", exception.Message);
        }

        [Fact]
        public async Task StepThatSkipsNext_ThrowsInvalidOperationExceptionAndSendsNothing()
        {
            var fakeMessageSender = new FakeMessageSender();
            var fakeMessageBusClient = new FakeMessageBusClient();
            using var serviceProvider = CreateServiceProvider(typeof(SkippingStep));
            var jobSubmitter = CreateSubmitter(serviceProvider, fakeMessageSender, fakeMessageBusClient, typeof(SkippingStep));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => jobSubmitter.SubmitJob(new SomeJobMessage()));

            Assert.Contains("send step", exception.Message);
            Assert.Contains(nameof(SomeJobMessage), exception.Message);
            Assert.Empty(fakeMessageSender.SentMessages);
            Assert.Empty(fakeMessageBusClient.CreatedSenders);
        }

        [Fact]
        public async Task StepPassingThrough_ReturnsJobIdCarriedByBothMessages()
        {
            var fakeMessageSender = new FakeMessageSender();
            var fakeMessageBusClient = new FakeMessageBusClient();
            using var serviceProvider = CreateServiceProvider(typeof(PassThroughStep));
            var jobSubmitter = CreateSubmitter(serviceProvider, fakeMessageSender, fakeMessageBusClient, typeof(PassThroughStep));

            var jobId = await jobSubmitter.SubmitJob(new SomeJobMessage());

            var createJobState = Assert.IsType<FlowlysysCreateJobStateMessage>(Assert.Single(fakeMessageSender.SentMessages));
            var (_, properties) = Assert.Single(fakeMessageBusClient.GetSender("some-job-queue").SentMessages);
            Assert.NotEqual(Guid.Empty, jobId.InnerId);
            Assert.Equal(jobId, createJobState.JobId);
            Assert.Equal(jobId.InnerId.ToString(), properties.MessageId);
        }

        [Fact]
        public async Task CreateJobStateMessage_DoesNotRunSteps()
        {
            var contexts = new List<SendStepContext<object>>();
            var services = new ServiceCollection();
            services.AddSingleton(contexts);
            services.AddScoped<ContextCapturingStep>();
            using var serviceProvider = services.BuildServiceProvider();
            var sendStepRunner = CreateRunner(serviceProvider, typeof(ContextCapturingStep));
            var runnerBackedMessageSender = new RunnerBackedMessageSender(sendStepRunner);
            var jobSubmitter = new JobSubmitter<SomeJobMessage>(
                new FakeMessageBusClientRegistry(new FakeMessageBusClient()),
                "some-job-queue",
                "primary",
                runnerBackedMessageSender,
                sendStepRunner);

            await jobSubmitter.SubmitJob(new SomeJobMessage());

            Assert.IsType<FlowlysysCreateJobStateMessage>(Assert.Single(runnerBackedMessageSender.SentMessages));
            Assert.IsType<SomeJobMessage>(Assert.Single(contexts).Message);
        }

        [Fact]
        public async Task PassesMessageAndCancellationTokenToTheStep()
        {
            var contexts = new List<SendStepContext<object>>();
            var services = new ServiceCollection();
            services.AddSingleton(contexts);
            services.AddScoped<ContextCapturingStep>();
            using var serviceProvider = services.BuildServiceProvider();
            var jobSubmitter = CreateSubmitter(serviceProvider, new FakeMessageSender(), new FakeMessageBusClient(), typeof(ContextCapturingStep));
            var message = new SomeJobMessage();
            using var cancellationTokenSource = new CancellationTokenSource();

            await jobSubmitter.SubmitJob(message, cancellationTokenSource.Token);

            var sendStepContext = Assert.Single(contexts);
            Assert.Same(message, sendStepContext.Message);
            Assert.Equal(cancellationTokenSource.Token, sendStepContext.CancellationToken);
        }

        [Fact]
        public async Task StepForAnotherMessageType_DoesNotRun()
        {
            var fakeMessageSender = new FakeMessageSender();
            using var serviceProvider = CreateServiceProvider(typeof(OtherMessageStep));
            var jobSubmitter = CreateSubmitter(serviceProvider, fakeMessageSender, new FakeMessageBusClient(), typeof(OtherMessageStep));

            await jobSubmitter.SubmitJob(new SomeJobMessage());

            Assert.Single(fakeMessageSender.SentMessages);
        }

        [Fact]
        public async Task WithoutRegisteredSteps_CreatesNoScope()
        {
            var fakeMessageSender = new FakeMessageSender();
            var jobSubmitter = new JobSubmitter<SomeJobMessage>(
                new FakeMessageBusClientRegistry(new FakeMessageBusClient()),
                "some-job-queue",
                "primary",
                fakeMessageSender,
                new SendStepRunner([], new ThrowingServiceScopeFactory()));

            await jobSubmitter.SubmitJob(new SomeJobMessage());

            Assert.Single(fakeMessageSender.SentMessages);
        }

        private static ServiceProvider CreateServiceProvider(params Type[] stepTypes)
        {
            var services = new ServiceCollection();

            foreach (var stepType in stepTypes)
            {
                services.AddScoped(stepType);
            }

            return services.BuildServiceProvider();
        }

        private static SendStepRunner CreateRunner(IServiceProvider serviceProvider, params Type[] stepTypes)
            => new(
                stepTypes.Select(stepType => new SendStepRegistration(stepType, SendStepRegistration.ResolveFilterType(stepType))),
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

        private static JobSubmitter<SomeJobMessage> CreateSubmitter(
            IServiceProvider serviceProvider,
            IMessageSender messageSender,
            FakeMessageBusClient messageBusClient,
            params Type[] stepTypes)
            => new(new FakeMessageBusClientRegistry(messageBusClient), "some-job-queue", "primary", messageSender, CreateRunner(serviceProvider, stepTypes));

        private static int CountJobMessages(FakeMessageBusClient messageBusClient)
            => messageBusClient.CreatedSenders.Contains("some-job-queue") ? messageBusClient.GetSender("some-job-queue").SentMessages.Count : 0;
    }

    private sealed class RunnerBackedMessageSender(ISendStepRunner sendStepRunner) : IMessageSender
    {
        public List<object> SentMessages { get; } = [];

        public Task Send<TMessage>(TMessage message, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string>? customProperties = null)
            => sendStepRunner.Run(message!, cancellationToken, () =>
            {
                SentMessages.Add(message!);

                return Task.CompletedTask;
            });
    }

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

    private sealed class OtherMessageStep : SendStep<OtherMessage>
    {
        public override Task Handle(SendStepContext<OtherMessage> context, SendStepDelegate next) => throw new UnauthorizedAccessException("rejected");
    }

    private sealed class ContextCapturingStep(List<SendStepContext<object>> contexts) : SendStep
    {
        public override Task Handle(SendStepContext<object> context, SendStepDelegate next)
        {
            contexts.Add(context);

            return next();
        }
    }

    private record OtherMessage;

    private record SomeJobMessage : IJobMessage
    {
        public string Description => "some description";
        public string JobTypeName => "SomeJob";
    }
}
