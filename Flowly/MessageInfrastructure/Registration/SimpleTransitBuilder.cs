using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

public class SimpleTransitBuilder : ISimpleTransitBuilder
{
    public SimpleTransitBuilder(IServiceCollection services, IReadOnlyList<string> args)
    {
        Services = services;
        Args = args;
    }

    public IServiceCollection Services { get; }
    public IReadOnlyList<string> Args { get; }
}