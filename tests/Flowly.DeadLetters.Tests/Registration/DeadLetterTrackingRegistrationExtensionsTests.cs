using Flowly.DeadLetters.BackgroundServices;
using Flowly.DeadLetters.Repositories;
using Flowly.DeadLetters.Services;
using Flowly.DeadLetters.Telemetry;
using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.DeadLetters.Tests.Registration;

public class DeadLetterTrackingRegistrationExtensionsTests
{
    public class AddDeadLetterTracking
    {
        [Fact]
        public void RegistersDeadLetterCleanupInstrumentation()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new FlowlyOptions());
            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddDeadLetterTracking(_ => { });

            Assert.NotNull(services.BuildServiceProvider().GetRequiredService<IDeadLetterCleanupInstrumentation>());
        }

        [Fact]
        public void RegistersDeadLetterCleanupBackgroundService()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new FlowlyOptions());
            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddDeadLetterTracking(_ => { });

            Assert.NotNull(services.FirstOrDefault(s => s.ImplementationType == typeof(DeadLetterCleanupBackgroundService)));
        }

        [Fact]
        public void RegistersDeadLetterMetricsBackgroundService()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new FlowlyOptions());
            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddDeadLetterTracking(_ => { });

            Assert.NotNull(services.FirstOrDefault(s => s.ImplementationType == typeof(DeadLetterMetricsBackgroundService)));
        }

        [Fact]
        public void RegistersDeadLetterIngestionHealthBackgroundService()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new FlowlyOptions());
            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddDeadLetterTracking(_ => { });

            Assert.NotNull(services.FirstOrDefault(s => s.ImplementationType == typeof(DeadLetterIngestionHealthBackgroundService)));
        }

        [Fact]
        public void RegistersDeadLetterBatchIngestor()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new FlowlyOptions());
            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddDeadLetterTracking(_ => { });

            Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(IDeadLetterBatchIngestor)));
        }

        [Fact]
        public void RegistersDeadLetterIngestionHealthChecker()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new FlowlyOptions());
            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddDeadLetterTracking(_ => { });

            Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(IDeadLetterIngestionHealthChecker)));
        }

        [Fact]
        public void WithTelemetryEnabled_RegistersRealOperationInstrumentation()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new FlowlyOptions { EnableTelemetry = true });
            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddDeadLetterTracking(_ => { });

            var instrumentation = services.BuildServiceProvider().GetRequiredService<IDeadLetterOperationInstrumentation>();
            Assert.IsType<DeadLetterOperationInstrumentation>(instrumentation);
        }

        [Fact]
        public void WithTelemetryDisabled_RegistersNullOperationInstrumentation()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new FlowlyOptions { EnableTelemetry = false });
            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddDeadLetterTracking(_ => { });

            var instrumentation = services.BuildServiceProvider().GetRequiredService<IDeadLetterOperationInstrumentation>();
            Assert.IsType<NullDeadLetterOperationInstrumentation>(instrumentation);
        }

        [Fact]
        public void WithTelemetryEnabled_RegistersRealCleanupInstrumentation()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new FlowlyOptions { EnableTelemetry = true });
            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddDeadLetterTracking(_ => { });

            var instrumentation = services.BuildServiceProvider().GetRequiredService<IDeadLetterCleanupInstrumentation>();
            Assert.IsType<DeadLetterCleanupInstrumentation>(instrumentation);
        }

        [Fact]
        public void WithTelemetryDisabled_RegistersNullCleanupInstrumentation()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new FlowlyOptions { EnableTelemetry = false });
            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddDeadLetterTracking(_ => { });

            var instrumentation = services.BuildServiceProvider().GetRequiredService<IDeadLetterCleanupInstrumentation>();
            Assert.IsType<NullDeadLetterCleanupInstrumentation>(instrumentation);
        }

        [Fact]
        public void CallingTwice_DoesNotRegisterDuplicateServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new FlowlyOptions());
            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddDeadLetterTracking(_ => { });
            flowlyBuilder.AddDeadLetterTracking(_ => { });

            Assert.Equal(1, services.Count(s => s.ImplementationType == typeof(DeadLetterCleanupBackgroundService)));
        }
    }

    public class WithDeadLetterTrackingForMessageHandler
    {
        private static IMessageHandlerBuilder<OrderPlaced> BuildMessageHandlerBuilder(string queueName, string providerName, ServiceCollection? services = null)
        {
            services ??= new ServiceCollection();
            services.AddSingleton<IHandlerSettings<OrderPlaced>>(new StubHandlerSettings(queueName, providerName));
            return new StubMessageHandlerBuilder<OrderPlaced>(services);
        }

        private static IMessageHandlerBuilder<OrderPlaced> BuildMessageHandlerBuilderWithDeadLetterTracking(string queueName, string providerName, ServiceCollection? services = null)
        {
            services ??= new ServiceCollection();
            var flowlyBuilder = new StubFlowlyBuilder(services);
            flowlyBuilder.AddDeadLetterTracking(_ => { });
            return BuildMessageHandlerBuilder(queueName, providerName, services);
        }

        [Fact]
        public void ThrowsInvalidOperation_WhenDeadLetterTrackingNotConfigured()
        {
            var builder = BuildMessageHandlerBuilder("orders", "primary");

            Assert.Throws<InvalidOperationException>(() => builder.WithDeadLetterTracking());
        }

        [Fact]
        public void RegistersIngestionSettings()
        {
            var builder = BuildMessageHandlerBuilderWithDeadLetterTracking("orders", "primary");

            builder.WithDeadLetterTracking();

            var settings = builder.Services
                .Where(s => s.ServiceType == typeof(DeadLetterIngestionSettings))
                .Select(s => s.ImplementationInstance)
                .OfType<DeadLetterIngestionSettings>()
                .FirstOrDefault();

            Assert.NotNull(settings);
            Assert.Equal("orders", settings.QueueName);
            Assert.Equal("primary", settings.ProviderName);
        }

        [Fact]
        public void RegistersIngestionBackgroundService()
        {
            var builder = BuildMessageHandlerBuilderWithDeadLetterTracking("orders", "primary");

            builder.WithDeadLetterTracking();

            var descriptor = builder.Services.FirstOrDefault(s => s.ImplementationType == typeof(DeadLetterIngestionBackgroundService));

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void CallingTwiceForSameQueue_DoesNotRegisterDuplicate()
        {
            var builder = BuildMessageHandlerBuilderWithDeadLetterTracking("orders", "primary");

            builder.WithDeadLetterTracking();
            builder.WithDeadLetterTracking();

            var count = builder.Services.Count(s => s.ServiceType == typeof(DeadLetterIngestionSettings));

            Assert.Equal(1, count);
        }
    }

    public class AddDeadLetterReadAccess
    {
        [Fact]
        public void RegistersDeadLetterService()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new FlowlyOptions());
            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddDeadLetterReadAccess(_ => { });

            Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(IDeadLetterService)));
        }

        [Fact]
        public void RegistersDeadLetterRepository()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new FlowlyOptions());
            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddDeadLetterReadAccess(_ => { });

            Assert.NotNull(services.FirstOrDefault(s => s.ImplementationType == typeof(DeadLetterRepository)));
        }

        [Fact]
        public void DoesNotRegisterIngestionOrCleanupBackgroundServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new FlowlyOptions());
            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddDeadLetterReadAccess(_ => { });

            Assert.DoesNotContain(services, s => s.ImplementationType == typeof(DeadLetterCleanupBackgroundService));
            Assert.DoesNotContain(services, s => s.ImplementationType == typeof(DeadLetterMetricsBackgroundService));
        }

        [Fact]
        public void WithTelemetryEnabled_RegistersRealOperationInstrumentation()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new FlowlyOptions { EnableTelemetry = true });
            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddDeadLetterReadAccess(_ => { });

            var instrumentation = services.BuildServiceProvider().GetRequiredService<IDeadLetterOperationInstrumentation>();
            Assert.IsType<DeadLetterOperationInstrumentation>(instrumentation);
        }
    }

    private static IEventHandlerBuilder<OrderPlaced> BuildEventHandlerBuilder(
        string topicName,
        string subscriptionName,
        string providerName,
        ServiceCollection? services = null)
    {
        services ??= new ServiceCollection();
        return new StubEventHandlerBuilder<OrderPlaced>(services, topicName, subscriptionName, providerName);
    }

    private static IEventHandlerBuilder<OrderPlaced> BuildEventHandlerBuilderWithDeadLetterTracking(
        string topicName,
        string subscriptionName,
        string providerName,
        ServiceCollection? services = null)
    {
        services ??= new ServiceCollection();
        var flowlyBuilder = new StubFlowlyBuilder(services);
        flowlyBuilder.AddDeadLetterTracking(_ => { });
        return new StubEventHandlerBuilder<OrderPlaced>(services, topicName, subscriptionName, providerName);
    }

    public class WithDeadLetterTrackingForEventHandler
    {
        [Fact]
        public void ThrowsInvalidOperation_WhenDeadLetterTrackingNotConfigured()
        {
            var builder = BuildEventHandlerBuilder("order-placed", "notification-handler", "primary");

            Assert.Throws<InvalidOperationException>(() => builder.WithDeadLetterTracking());
        }

        [Fact]
        public void RegistersIngestionSettings()
        {
            var builder = BuildEventHandlerBuilderWithDeadLetterTracking("order-placed", "notification-handler", "primary");

            builder.WithDeadLetterTracking();

            var settings = builder.Services
                .Where(s => s.ServiceType == typeof(EventSubscriptionDeadLetterIngestionSettings))
                .Select(s => s.ImplementationInstance)
                .OfType<EventSubscriptionDeadLetterIngestionSettings>()
                .FirstOrDefault();

            Assert.NotNull(settings);
            Assert.Equal("order-placed", settings.TopicName);
            Assert.Equal("notification-handler", settings.SubscriptionName);
            Assert.Equal("primary", settings.ProviderName);
        }

        [Fact]
        public void RegistersIngestionBackgroundService()
        {
            var builder = BuildEventHandlerBuilderWithDeadLetterTracking("order-placed", "notification-handler", "primary");

            builder.WithDeadLetterTracking();

            var descriptor = builder.Services.FirstOrDefault(s =>
                s.ImplementationType == typeof(EventSubscriptionDeadLetterIngestionBackgroundService));

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void CallingTwiceForSameSubscription_DoesNotRegisterDuplicate()
        {
            var builder = BuildEventHandlerBuilderWithDeadLetterTracking("order-placed", "notification-handler", "primary");

            builder.WithDeadLetterTracking();
            builder.WithDeadLetterTracking();

            var settingsCount = builder.Services
                .Count(s => s.ServiceType == typeof(EventSubscriptionDeadLetterIngestionSettings));

            Assert.Equal(1, settingsCount);
        }

        [Fact]
        public void TwoDistinctSubscriptions_BothRegistered()
        {
            var services = new ServiceCollection();
            var builderA = BuildEventHandlerBuilderWithDeadLetterTracking("order-placed", "notification-handler", "primary", services);
            var builderB = BuildEventHandlerBuilderWithDeadLetterTracking("order-placed", "audit-log-handler", "primary", services);

            builderA.WithDeadLetterTracking();
            builderB.WithDeadLetterTracking();

            var settingsCount = services
                .Count(s => s.ServiceType == typeof(EventSubscriptionDeadLetterIngestionSettings));

            Assert.Equal(2, settingsCount);
        }
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }

    private sealed class StubEventHandlerBuilder<TEvent>(
        IServiceCollection services,
        string topicName,
        string subscriptionName,
        string providerName) : IEventHandlerBuilder<TEvent>
        where TEvent : class
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
        public string TopicName => topicName;
        public string SubscriptionName => subscriptionName;
        public string ProviderName => providerName;
    }

    private sealed class StubMessageHandlerBuilder<TMessage>(IServiceCollection services) : IMessageHandlerBuilder<TMessage>
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }

    private sealed class StubHandlerSettings(string queueName, string providerName) : IHandlerSettings<OrderPlaced>
    {
        public string QueueName => queueName;
        public string ProviderName => providerName;
        public string HandlerName => "test-handler";
        public bool ReadAndDelete => false;
        public int MaxConcurrentCalls => 1;
        public int MaxRetries => 0;
        public int RetryDelaySeconds => 0;
        public int MaxMessagesBeforeProcessing => 1;
        public TimeSpan MaxWaitTime => TimeSpan.FromSeconds(30);
    }

    private record OrderPlaced;
}