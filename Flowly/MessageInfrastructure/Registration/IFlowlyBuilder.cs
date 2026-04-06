using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

public interface IFlowlyBuilder
{
    IServiceCollection Services { get; }
    IConfiguration Configuration { get; }
}