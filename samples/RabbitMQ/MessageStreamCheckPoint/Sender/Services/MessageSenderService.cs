using Flowly;
using Messages;

namespace Sender.Services;

internal class MessageSenderService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int messageIndex = 0;

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MessageSenderService>>();
        var recorder = scope.ServiceProvider.GetRequiredService<IMessageRecorder>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var partitionKey = $"sensor-{DateTime.Now.Second % 4}";

            var message = new MyMessage(++messageIndex, $"Hello from Sender at {DateTime.Now}");
            await recorder.Record(message, stoppingToken, partitionKey);

            logger.LogInformation("Message {Index} - {Text} sent", message.MessageNumber, message.Text);

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}