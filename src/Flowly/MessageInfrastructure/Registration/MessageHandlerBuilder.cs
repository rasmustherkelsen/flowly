using Flowly.MessageInfrastructure.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

internal class MessageHandlerBuilder<TMessage>(IFlowlyBuilder flowlyBuilder, IHandlerSettings<TMessage> handlerSettings) : IMessageHandlerBuilder<TMessage>
{
    public IServiceCollection Services => flowlyBuilder.Services;

    public IConfiguration Configuration => flowlyBuilder.Configuration;

    public IHandlerSettings<TMessage> HandlerSettings => handlerSettings;
}