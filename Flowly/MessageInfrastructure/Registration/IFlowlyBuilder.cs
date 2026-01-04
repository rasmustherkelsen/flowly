using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

public interface IFlowlyBuilder
{
    IServiceCollection Services { get; }
}