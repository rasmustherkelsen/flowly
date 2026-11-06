using Flowly.MessageInfrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.RabbitMQ.Tests.Fakes;

internal sealed class FakeFlowlyBuilder(IServiceCollection services, IConfiguration configuration) : IFlowlyBuilder
{
    public IServiceCollection Services => services;
    public IConfiguration Configuration => configuration;
    public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
}
