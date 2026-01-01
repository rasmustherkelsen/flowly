using Microsoft.Extensions.DependencyInjection;

namespace SimpleTransit.MessageInfrastructure.Registration;

public interface ISimpleTransitBuilder
{
    IServiceCollection Services { get; }
}