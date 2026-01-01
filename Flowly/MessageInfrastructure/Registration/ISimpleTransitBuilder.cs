using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

public interface ISimpleTransitBuilder
{
    IServiceCollection Services { get; }
}