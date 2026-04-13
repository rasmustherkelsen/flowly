namespace Flowly.MessageInfrastructure.Registration;

public sealed class ProviderQueueManifest
{
    private readonly List<DeferredQueueRegistration> _queues = [];

    public ProviderQueueManifest(string providerName, bool isPrimary, string transportType)
    {
        ProviderName = providerName;
        IsPrimary = isPrimary;
        TransportType = transportType;
    }

    public string ProviderName { get; }

    public bool IsPrimary { get; }

    public string TransportType { get; }

    public IReadOnlyList<DeferredQueueRegistration> Queues => _queues;

    internal void Add(DeferredQueueRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(registration.QueueName))
            return;

        var existing = _queues.FindIndex(q =>
            string.Equals(q.QueueName, registration.QueueName, StringComparison.OrdinalIgnoreCase));

        if (existing < 0)
        {
            _queues.Add(registration);
            return;
        }

        _queues[existing] = Merge(_queues[existing], registration);
    }

    private static DeferredQueueRegistration Merge(DeferredQueueRegistration a, DeferredQueueRegistration b)
    {
        return new DeferredQueueRegistration(
            a.QueueName,
            a.RequiresSession || b.RequiresSession,
            MergeSetting(a.DefaultMessageTimeToLive, b.DefaultMessageTimeToLive, a.QueueName, nameof(DeferredQueueRegistration.DefaultMessageTimeToLive)),
            MergeSetting(a.DeadLetterOnMessageExpiration, b.DeadLetterOnMessageExpiration, a.QueueName, nameof(DeferredQueueRegistration.DeadLetterOnMessageExpiration)),
            MergeSetting(a.LockDuration, b.LockDuration, a.QueueName, nameof(DeferredQueueRegistration.LockDuration)));
    }

    private static T? MergeSetting<T>(T? a, T? b, string queueName, string settingName) where T : struct
    {
        if (a is null) return b;
        if (b is null) return a;

        if (EqualityComparer<T>.Default.Equals(a.Value, b.Value))
            return a;

        throw new InvalidOperationException(
            $"Conflicting queue setting '{settingName}' for queue '{queueName}' on provider '{settingName}'.");
    }
}
