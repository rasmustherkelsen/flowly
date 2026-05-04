using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

internal class FlowlyBuilder(
    IServiceCollection services,
    IConfiguration configuration,
    ITopologyNameResolver topologyNameResolver) : IFlowlyBuilder
{
    public IServiceCollection Services { get; } = services;

    public IConfiguration Configuration { get; } = configuration;

    public ITopologyNameResolver TopologyNameResolver { get; } = topologyNameResolver;
}