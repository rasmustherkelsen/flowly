using Flowly.MessageInfrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageStreams.Tests.Fakes;

internal sealed class StubFlowlyBuilder(IServiceCollection services, IConfiguration configuration) : IFlowlyBuilder
{
    public StubFlowlyBuilder() : this(new ServiceCollection(), new ConfigurationBuilder().Build())
    {
    }

    public IServiceCollection Services => services;

    public IConfiguration Configuration => configuration;

    public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
}
