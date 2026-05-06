using Flowly.MessageInfrastructure.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

internal class MessageHandlerBuilder<TMessage>(IFlowlyBuilder flowlyBuilder) : IMessageHandlerBuilder<TMessage>
{
    public IServiceCollection Services => flowlyBuilder.Services;

    public IConfiguration Configuration => flowlyBuilder.Configuration;

    public ITopologyNameResolver TopologyNameResolver => flowlyBuilder.TopologyNameResolver;
}