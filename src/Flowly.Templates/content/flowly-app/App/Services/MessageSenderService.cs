using Flowly;
using App.Messages;

namespace App.Services;

#if (UseCallHandler)
internal class MessageSenderService(IServiceScopeFactory serviceScopeFactory, ILogger<MessageSenderService> logger) : BackgroundService
#else
internal class MessageSenderService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
#endif
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();

#if (UseCallHandler)
        var caller = scope.ServiceProvider.GetRequiredService<IMessageCaller>();
#else
#if (UseStream)
        var recorder = scope.ServiceProvider.GetRequiredService<IMessageRecorder>();
#else
        var sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
#endif
#endif

        while (!stoppingToken.IsCancellationRequested)
        {
#if (UseCallHandler)
            MyReturnMessage response = await caller.Call<MyMessage, MyReturnMessage>(
                new MyMessage($"Hello at {DateTime.Now}"), stoppingToken);

            logger.LogInformation("Received response: {Reply}", response.Reply);
#else
#if (UseStream)
#if (UseStreamPartitions)
            var partitionKey = $"sensor-{DateTime.Now.Second % 4}";

            await recorder.Record(new MyMessage($"Hello at {DateTime.Now}"), stoppingToken, partitionKey);
#else
            await recorder.Record(new MyMessage($"Hello at {DateTime.Now}"), stoppingToken);
#endif
#else
            await sender.Send(new MyMessage($"Hello at {DateTime.Now}"), stoppingToken);
#endif
#endif

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
