using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.Model;
using Flowly.Jobs.Receivers;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

public static class JobHandlerRegistrationExtensions
{
    public static IFlowlyBuilder AddJobHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder)
        where THandler : JobMessageHandlerBase<TMessage>
        where TMessage : class, IJobMessage
    {
        using var serviceProvider = flowlyBuilder.Services.BuildServiceProvider();
        var handlerSettingsFactory = serviceProvider.GetRequiredService<IHandlerSettingsFactory>();
        var queueRegistrar = serviceProvider.GetRequiredService<IQueueRegistrar>();

        var registration = handlerSettingsFactory.Create<THandler, TMessage>(flowlyBuilder.Services);
        queueRegistrar.Register(flowlyBuilder.Services, registration.QueueRegistration, registration.HandlerSettings.ProviderName);

        flowlyBuilder.Services
            .AddScoped<THandler>()
            .AddScoped<JobMessageHandlerBase<TMessage>, THandler>()
            .AddSingleton(registration.HandlerSettings)
            .AddMessageProcessingPipeline(
                registration.HandlerSettings,
                sp => new JobMessageHandlingStrategy<TMessage>(sp.GetRequiredService<IServiceScopeFactory>()));

        flowlyBuilder.AddJobStateSubmitters();

        return flowlyBuilder;
    }
}