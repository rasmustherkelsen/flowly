using Azure.Storage.Blobs;
using Flowly.MessageStreams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.AzureEventHubs.Tests.Emulator.Support;

internal sealed record EmulatorHostOptions(
    HandlerSettings Settings,
    BlobContainerClient? PartitionOwnershipContainer = null,
    TimeSpan? LoadBalancingInterval = null,
    TimeSpan? PartitionOwnershipExpiration = null,
    string? SqliteCheckpointPath = null,
    Action<IFlowlyBuilder>? Configure = null,
    string? ConnectionString = null);

internal sealed class EmulatorHost<TMessage> : IAsyncDisposable where TMessage : class, ITaggedEvent<TMessage>
{
    private readonly IHost _host;
    private bool _started;

    private EmulatorHost(IHost host, HandlerBehavior<TMessage> behavior, RecordingLoggerProvider logs)
    {
        _host = host;
        Behavior = behavior;
        Logs = logs;
    }

    public HandlerBehavior<TMessage> Behavior { get; }

    public RecordingLoggerProvider Logs { get; }

    public static EmulatorHost<TMessage> Build<THandler>(EmulatorHostOptions options) where THandler : RecordingHandler<TMessage>
    {
        var behavior = new HandlerBehavior<TMessage>();
        var logs = new RecordingLoggerProvider();
        var builder = Host.CreateApplicationBuilder();

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Logging.AddProvider(logs);
        builder.Services.Configure<HostOptions>(hostOptions => hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
        builder.Services.AddSingleton(behavior);

        RecordingHandler<TMessage>.UseSettings(options.Settings);

        builder.AddFlowly(flowly =>
        {
            flowly.UseAzureEventHubs(
                options.ConnectionString ?? EmulatorEnvironment.RequiredConnectionString,
                partitionOwnershipContainer: options.PartitionOwnershipContainer,
                loadBalancingInterval: options.LoadBalancingInterval,
                partitionOwnershipExpiration: options.PartitionOwnershipExpiration);
            flowly.AddMessageRecorder<TMessage>();
            flowly.AddMessageStreamHandler<TMessage, THandler>();

            if (options.SqliteCheckpointPath is { } sqliteCheckpointPath)
            {
                flowly.AddSQLiteMessageStreamPersistence($"Data Source={sqliteCheckpointPath}");
                flowly.UseMessageStreamCheckpoint<TMessage>();
            }

            options.Configure?.Invoke(flowly);
        });

        return new EmulatorHost<TMessage>(builder.Build(), behavior, logs);
    }

    public static EmulatorHost<TMessage> Build(EmulatorHostOptions options) => Build<RecordingHandler<TMessage>>(options);

    public async Task Start()
    {
        _started = true;

        await _host.StartAsync();
    }

    public async Task Record(TMessage message, string? partitionKey = null)
    {
        await using var scope = _host.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IMessageRecorder>().Record(message, CancellationToken.None, partitionKey);
    }

    public async Task RecordMany(string tag, int count, string? partitionKey = null)
    {
        foreach (var number in Enumerable.Range(0, count))
        {
            await Record(TMessage.Create(tag, number), partitionKey);
        }
    }

    public async Task Stop()
    {
        if (!_started)
            return;

        _started = false;

        await _host.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Stop();

        _host.Dispose();
    }
}
