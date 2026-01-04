using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

internal class FlowlyBuilder(IServiceCollection services, IReadOnlyList<string> args) : IFlowlyBuilder
{
    public IServiceCollection Services { get; } = services;
    public IReadOnlyList<string> Args { get; } = args;
}