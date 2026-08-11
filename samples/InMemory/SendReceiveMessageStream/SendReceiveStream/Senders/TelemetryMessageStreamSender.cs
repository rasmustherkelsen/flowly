using Flowly;
using SendReceiveStream.Messages;

namespace SendReceiveStream.Senders;

internal class TelemetryMessageStreamSender(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var messageSender = scope.ServiceProvider.GetRequiredService<IMessageRecorder>();
        
        long messageNumber = 0;
        
        while (!stoppingToken.IsCancellationRequested)
        {
            var message = new TelemetryMessage(++messageNumber);
            await messageSender.Record(message, stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}