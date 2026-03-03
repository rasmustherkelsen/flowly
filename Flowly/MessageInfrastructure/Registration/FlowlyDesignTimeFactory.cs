using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

public abstract class FlowlyDesignTimeFactory
{
    private IFlowlyBuilder CreateBuilder()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        return new FlowlyBuilder(services, configuration);
    }

    protected IFlowlyBuilder CreateBuilder<TFlowlyConfiguration>() where TFlowlyConfiguration : IFlowlyConfiguration, new()
    {
        var builder = CreateBuilder();
        var module = new TFlowlyConfiguration();
        module.Configure(builder);
        return builder;
    }
}