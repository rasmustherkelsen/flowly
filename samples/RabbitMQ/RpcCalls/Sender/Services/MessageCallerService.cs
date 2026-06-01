using Flowly;
using Messages;

namespace Sender.Services;

internal class MessageCallerService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();

            var messageCaller = scope.ServiceProvider.GetRequiredService<IMessageCaller>();

            ReturnMessage returnMessage = await messageCaller.Call<CallMessage, ReturnMessage>(new CallMessage($"Hello from Sender at {DateTime.Now}"), stoppingToken);
            
            Console.WriteLine($"Received response: {returnMessage.ReturnValue}");

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
