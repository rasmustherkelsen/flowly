using Microsoft.Extensions.Hosting;

namespace Flowly.Services;

public class CommandLineParserHostedService : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Flowly CLI Hook");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken _) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken _) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken _) => Task.CompletedTask;

    public Task StopAsync(CancellationToken _) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken _) => Task.CompletedTask;
}