namespace Flowly.AzureEventHubs.Tests.Fakes;

internal sealed class FakeMessageStreamCheckpoint<TMessage> : MessageStreamCheckpoint<TMessage>
{
    private const int NonPartitioned = -1;

    private readonly Dictionary<int, long> _positions = [];
    private readonly Lock _gate = new();

    public List<(int? Partition, long Position, bool IsBootstrapSeed)> Saves { get; } = [];

    public void Store(int? partition, long position)
    {
        lock (_gate)
        {
            _positions[partition ?? NonPartitioned] = position;
        }
    }

    protected internal override Task InitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    protected internal override Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<long?>(_positions.TryGetValue(context.Partition ?? NonPartitioned, out var position) ? position : null);
        }
    }

    protected internal override Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _positions[context.Partition ?? NonPartitioned] = context.Position;
            Saves.Add((context.Partition, context.Position, context.IsBootstrapSeed));
        }

        return Task.CompletedTask;
    }
}
