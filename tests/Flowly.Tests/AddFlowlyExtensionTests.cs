using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Events.Telemetry;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Registration;
using Flowly.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.Tests;

public class AddFlowlyExtensionTests
{
    public class AddFlowly_Generic
    {
        [Fact]
        public void RegistersMessageBusClientRegistry()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            var descriptor = hostApplicationBuilder.Services.Single(s => s.ServiceType == typeof(IMessageBusClientRegistry));
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void RegistersMessagingTopologyCreatorRegistry()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            Assert.Contains(hostApplicationBuilder.Services, s => s.ServiceType == typeof(IMessagingTopologyCreatorRegistry));
        }

        [Fact]
        public void RegistersEventTopologyCreatorRegistry()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            Assert.Contains(hostApplicationBuilder.Services, s => s.ServiceType == typeof(IEventTopologyCreatorRegistry));
        }

        [Fact]
        public void RegistersQueueManager()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            Assert.Contains(hostApplicationBuilder.Services, s => s.ServiceType == typeof(IQueueManager));
        }

        [Fact]
        public void RegistersCrossProviderConflictValidator()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            Assert.Contains(hostApplicationBuilder.Services, s => s.ServiceType == typeof(ICrossProviderConflictValidator));
        }

        [Fact]
        public void RegistersHandlerSettingsFactory()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            Assert.Contains(hostApplicationBuilder.Services, s => s.ServiceType == typeof(IHandlerSettingsFactory));
        }

        [Fact]
        public void RegistersQueueRegistrar()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            Assert.Contains(hostApplicationBuilder.Services, s => s.ServiceType == typeof(IQueueRegistrar));
        }

        [Fact]
        public void RegistersFlowlyOptionsAsSingleton()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            var descriptor = hostApplicationBuilder.Services.Single(s => s.ServiceType == typeof(FlowlyOptions));
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void RegistersTopologyNameResolverAsSingleton()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            var descriptor = hostApplicationBuilder.Services.Single(s => s.ServiceType == typeof(ITopologyNameResolver));
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void DefaultTopologyNameResolverIsKebabCase()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            using var serviceProvider = hostApplicationBuilder.Services.BuildServiceProvider();
            var topologyNameResolver = serviceProvider.GetRequiredService<ITopologyNameResolver>();

            Assert.IsType<KebabCaseTopologyNameResolver>(topologyNameResolver);
        }

        [Fact]
        public void TelemetryEnabled_RegistersHandlerInstrumentation()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            using var serviceProvider = hostApplicationBuilder.Services.BuildServiceProvider();
            var handlerInstrumentation = serviceProvider.GetRequiredService<IHandlerInstrumentation>();

            Assert.IsType<HandlerInstrumentation>(handlerInstrumentation);
        }

        [Fact]
        public void TelemetryEnabled_RegistersSubmitterInstrumentation()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            using var serviceProvider = hostApplicationBuilder.Services.BuildServiceProvider();
            var submitterInstrumentation = serviceProvider.GetRequiredService<ISubmitterInstrumentation>();

            Assert.IsType<SubmitterInstrumentation>(submitterInstrumentation);
        }

        [Fact]
        public void TelemetryEnabled_RegistersEventHandlerInstrumentation()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            using var serviceProvider = hostApplicationBuilder.Services.BuildServiceProvider();
            var eventHandlerInstrumentation = serviceProvider.GetRequiredService<IEventHandlerInstrumentation>();

            Assert.IsType<EventHandlerInstrumentation>(eventHandlerInstrumentation);
        }

        [Fact]
        public void TelemetryEnabled_RegistersEventPublisherInstrumentation()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            using var serviceProvider = hostApplicationBuilder.Services.BuildServiceProvider();
            var eventPublisherInstrumentation = serviceProvider.GetRequiredService<IEventPublisherInstrumentation>();

            Assert.IsType<EventPublisherInstrumentation>(eventPublisherInstrumentation);
        }

        [Fact]
        public void TelemetryDisabled_RegistersNullHandlerInstrumentation()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>(o => o.EnableTelemetry = false);

            using var serviceProvider = hostApplicationBuilder.Services.BuildServiceProvider();
            var handlerInstrumentation = serviceProvider.GetRequiredService<IHandlerInstrumentation>();

            Assert.IsType<NullHandlerInstrumentation>(handlerInstrumentation);
        }

        [Fact]
        public void TelemetryDisabled_RegistersNullSubmitterInstrumentation()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>(o => o.EnableTelemetry = false);

            using var serviceProvider = hostApplicationBuilder.Services.BuildServiceProvider();
            var submitterInstrumentation = serviceProvider.GetRequiredService<ISubmitterInstrumentation>();

            Assert.IsType<NullSubmitterInstrumentation>(submitterInstrumentation);
        }

        [Fact]
        public void TelemetryDisabled_RegistersNullEventHandlerInstrumentation()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>(o => o.EnableTelemetry = false);

            using var serviceProvider = hostApplicationBuilder.Services.BuildServiceProvider();
            var eventHandlerInstrumentation = serviceProvider.GetRequiredService<IEventHandlerInstrumentation>();

            Assert.IsType<NullEventHandlerInstrumentation>(eventHandlerInstrumentation);
        }

        [Fact]
        public void TelemetryDisabled_RegistersNullEventPublisherInstrumentation()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>(o => o.EnableTelemetry = false);

            using var serviceProvider = hostApplicationBuilder.Services.BuildServiceProvider();
            var eventPublisherInstrumentation = serviceProvider.GetRequiredService<IEventPublisherInstrumentation>();

            Assert.IsType<NullEventPublisherInstrumentation>(eventPublisherInstrumentation);
        }

        [Fact]
        public void RegistersCommandLineParserHostedService()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            Assert.Contains(hostApplicationBuilder.Services, s =>
                s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType == typeof(CommandLineParserHostedService));
        }

        [Fact]
        public void RegistersQueueRegistrarHostedService()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            Assert.Contains(hostApplicationBuilder.Services, s =>
                s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType == typeof(QueueRegistrarHostedService));
        }

        [Fact]
        public void InvokesConfigureOnConfiguration()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            Assert.True(RecordingFlowlyConfiguration.WasConfigured);
            RecordingFlowlyConfiguration.WasConfigured = false;
        }

        [Fact]
        public void ReturnsSameBuilder()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            var returnedBuilder = hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            Assert.Same(hostApplicationBuilder, returnedBuilder);
        }

        [Fact]
        public void ConfigureOptionsCallback_IsInvoked()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();
            var callbackInvoked = false;

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>(_ => callbackInvoked = true);

            Assert.True(callbackInvoked);
        }

        [Fact]
        public void WhenConfigOverridesInstanceName_SetsFlowlyOptionsInstanceNameAutomatically()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<ConfigWithInstanceNameOverride>();

            using var serviceProvider = hostApplicationBuilder.Services.BuildServiceProvider();
            var options = serviceProvider.GetRequiredService<FlowlyOptions>();
            Assert.Equal("my-service", options.InstanceName);
        }

        [Fact]
        public void WhenDelegateAlsoSetsInstanceName_DelegateTakesPriorityOverClassProperty()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<ConfigWithInstanceNameOverride>(o => o.InstanceName = "from-delegate");

            using var serviceProvider = hostApplicationBuilder.Services.BuildServiceProvider();
            var options = serviceProvider.GetRequiredService<FlowlyOptions>();
            Assert.Equal("from-delegate", options.InstanceName);
        }

        [Fact]
        public void WhenConfigDoesNotOverrideInstanceName_FlowlyOptionsInstanceNameIsNull()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>();

            using var serviceProvider = hostApplicationBuilder.Services.BuildServiceProvider();
            var options = serviceProvider.GetRequiredService<FlowlyOptions>();
            Assert.Null(options.InstanceName);
        }

        [Fact]
        public void CustomTopologyNameResolver_IsRegistered()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly<RecordingFlowlyConfiguration>(o => o.WithTopologyNameResolver<CustomTopologyNameResolver>());

            using var serviceProvider = hostApplicationBuilder.Services.BuildServiceProvider();
            var topologyNameResolver = serviceProvider.GetRequiredService<ITopologyNameResolver>();

            Assert.IsType<CustomTopologyNameResolver>(topologyNameResolver);
        }
    }

    public class AddFlowly_Inline
    {
        [Fact]
        public void InvokesInlineConfigureCallback()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();
            var inlineCalled = false;

            hostApplicationBuilder.AddFlowly(configureOptions: null, configure: _ => inlineCalled = true);

            Assert.True(inlineCalled);
        }

        [Fact]
        public void RegistersAllCoreServices()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly(configureOptions: null, configure: _ => { });

            Assert.Contains(hostApplicationBuilder.Services, s => s.ServiceType == typeof(IMessageBusClientRegistry));
            Assert.Contains(hostApplicationBuilder.Services, s => s.ServiceType == typeof(IQueueManager));
        }

        [Fact]
        public void ReturnsSameBuilder()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            var returnedBuilder = hostApplicationBuilder.AddFlowly(configureOptions: null, configure: _ => { });

            Assert.Same(hostApplicationBuilder, returnedBuilder);
        }

        [Fact]
        public void ConfigureOptionsCallback_IsInvoked()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();
            var optionsCallbackInvoked = false;

            hostApplicationBuilder.AddFlowly(o => { optionsCallbackInvoked = true; o.EnableTelemetry = false; }, _ => { });

            Assert.True(optionsCallbackInvoked);
        }

        [Fact]
        public void TelemetryDisabled_RegistersNullHandlerInstrumentation()
        {
            var hostApplicationBuilder = new StubHostApplicationBuilder();

            hostApplicationBuilder.AddFlowly(o => o.EnableTelemetry = false, _ => { });

            using var serviceProvider = hostApplicationBuilder.Services.BuildServiceProvider();
            var handlerInstrumentation = serviceProvider.GetRequiredService<IHandlerInstrumentation>();

            Assert.IsType<NullHandlerInstrumentation>(handlerInstrumentation);
        }
    }

    private sealed class CustomTopologyNameResolver : ITopologyNameResolver
    {
        public string ResolveQueueName<TMessage>() => "custom-queue";
        public string ResolveEventName<TEvent>() => "custom-event";
        public string ResolveSubscriptionName<THandler>() => "custom-subscription";
        public string ResolveReplyQueueName(string callQueueName, string instanceName) => $"{callQueueName}-reply-{instanceName}";
    }

    private sealed class ConfigWithInstanceNameOverride : FlowlyDesignTimeFactory, IFlowlyConfiguration
    {
        public override string? InstanceName => "my-service";

        public void Configure(IFlowlyBuilder builder) { }
    }

    private sealed class RecordingFlowlyConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
    {
        public static bool WasConfigured { get; set; }

        public void Configure(IFlowlyBuilder builder)
        {
            WasConfigured = true;
        }
    }

    private sealed class StubHostApplicationBuilder : IHostApplicationBuilder
    {
        public IDictionary<object, object> Properties { get; } = new Dictionary<object, object>();
        public IConfigurationManager Configuration { get; } = new ConfigurationManager();
        public IHostEnvironment Environment { get; } = new StubHostEnvironment();
        public ILoggingBuilder Logging { get; } = new StubLoggingBuilder();
        public IMetricsBuilder Metrics { get; } = new StubMetricsBuilder();
        public IServiceCollection Services { get; } = new ServiceCollection();

        public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null) where TContainerBuilder : notnull
            => throw new NotSupportedException();
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "test";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Production";
    }

    private sealed class StubLoggingBuilder : ILoggingBuilder
    {
        public IServiceCollection Services { get; } = new ServiceCollection();
    }

    private sealed class StubMetricsBuilder : IMetricsBuilder
    {
        public IServiceCollection Services { get; } = new ServiceCollection();
    }
}
