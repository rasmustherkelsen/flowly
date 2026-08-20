using Flowly;
using MyAspireApp.Messages;

namespace MyAspireApp.Sender.Services;

#if (UseCallHandler)
internal class MessageSenderService(IServiceScopeFactory serviceScopeFactory, ILogger<MessageSenderService> logger) : BackgroundService
#else
internal class MessageSenderService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
#endif
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();

#if (UseCallHandler)
            var caller = scope.ServiceProvider.GetRequiredService<IMessageCaller>();

            MyReturnMessage response = await caller.Call<MyMessage, MyReturnMessage>(
                new MyMessage($"Hello from Sender at {DateTime.Now}"), stoppingToken);

            logger.LogInformation("Received response: {Reply}", response.Reply);
#else
#if (UseStream)
            var recorder = scope.ServiceProvider.GetRequiredService<IMessageRecorder>();

#if (UseStreamPartitions)
            var partitionKey = $"sensor-{DateTime.Now.Second % 4}";

            await recorder.Record(new MyMessage($"Hello from Sender at {DateTime.Now}"), stoppingToken, partitionKey);
#else
            await recorder.Record(new MyMessage($"Hello from Sender at {DateTime.Now}"), stoppingToken);
#endif
#else
            var sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

            await sender.Send(new MyMessage($"Hello from Sender at {DateTime.Now}"), stoppingToken);
#endif
#endif

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
