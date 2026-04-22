using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.Registration;

public static class MessageProcessingPipelineExtensions
{
    public static IServiceCollection AddMessageProcessingPipeline<TMessage>(
        this IServiceCollection services,
        IHandlerSettings<TMessage> handlerSettings,
        Func<IServiceProvider, IMessageHandlingStrategy<TMessage>> strategyFactory) where TMessage : class
    {
        return services.AddHostedService(sp => new MessageProcessingBackgroundService<TMessage>(
            sp.GetRequiredService<IMessageBusClientRegistry>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            handlerSettings,
            sp.GetRequiredService<ILogger<MessageProcessingBackgroundService<TMessage>>>(),
            sp.GetRequiredService<IHandlerInstrumentation>(),
            strategyFactory(sp)));
    }
}