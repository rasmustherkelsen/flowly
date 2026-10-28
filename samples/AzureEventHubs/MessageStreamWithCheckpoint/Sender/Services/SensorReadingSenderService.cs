using Flowly;
using Messages;

namespace Sender.Services;

internal class SensorReadingSenderService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private const int SensorCount = 6;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sequence = 0;

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SensorReadingSenderService>>();
        var recorder = scope.ServiceProvider.GetRequiredService<IMessageRecorder>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var sensorId = $"sensor-{sequence % SensorCount}";
            var reading = new SensorReadingMessage(sensorId, 20 + Random.Shared.NextDouble() * 5, ++sequence);

            await recorder.Record(reading, stoppingToken, partitionKey: sensorId);

            logger.LogInformation("Recorded reading {Sequence} of {SensorId}: {Celsius:F1} C", reading.Sequence, reading.SensorId, reading.Celsius);

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
