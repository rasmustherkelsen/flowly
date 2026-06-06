using System.Collections.Concurrent;
using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

internal class QueueManager : IQueueManager
{
    private readonly ConcurrentDictionary<string, DeferredQueueRegistration> _queues = new(StringComparer.OrdinalIgnoreCase);

    private List<IQueueDescription>? _queueDescriptions;

    public void RegisterQueue(DeferredQueueRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(registration.QueueName)) return;

        _queues.AddOrUpdate(
            registration.QueueName,
            registration,
            (_, existingRegistration) => Merge(existingRegistration, registration));
    }

    public IReadOnlyList<IQueueDescription> GetRegisteredQueues()
    {
        if (_queueDescriptions == null)
        {
            _queueDescriptions = new List<IQueueDescription>();
            foreach (var registration in _queues.Values.OrderBy(x => x.QueueName, StringComparer.OrdinalIgnoreCase))
            {
                if (registration.IsReplyQueue)
                {
                    _queueDescriptions.Add(new ReplyQueueDescription(registration.QueueName));
                    continue;
                }

                _queueDescriptions.Add(new QueueDescription(
                    registration.QueueName,
                    registration.DefaultMessageTimeToLive ?? TimeSpan.FromDays(1),
                    registration.DeadLetterOnMessageExpiration ?? true,
                    registration.LockDuration ?? TimeSpan.FromMinutes(5),
                    registration.RequiresSession));
            }
        }

        return _queueDescriptions;
    }

    private static DeferredQueueRegistration Merge(DeferredQueueRegistration existingRegistration, DeferredQueueRegistration newRegistration)
    {
        if (!string.Equals(existingRegistration.QueueName, newRegistration.QueueName, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Cannot merge queue registrations with different names.");

        var defaultMessageTimeToLive = MergeSetting(
            existingRegistration.DefaultMessageTimeToLive,
            newRegistration.DefaultMessageTimeToLive,
            existingRegistration.QueueName,
            nameof(DeferredQueueRegistration.DefaultMessageTimeToLive));

        var deadLetterOnMessageExpiration = MergeSetting(
            existingRegistration.DeadLetterOnMessageExpiration,
            newRegistration.DeadLetterOnMessageExpiration,
            existingRegistration.QueueName,
            nameof(DeferredQueueRegistration.DeadLetterOnMessageExpiration));

        var lockDuration = MergeSetting(
            existingRegistration.LockDuration,
            newRegistration.LockDuration,
            existingRegistration.QueueName,
            nameof(DeferredQueueRegistration.LockDuration));

        return new DeferredQueueRegistration(
            existingRegistration.QueueName,
            existingRegistration.RequiresSession || newRegistration.RequiresSession,
            defaultMessageTimeToLive,
            deadLetterOnMessageExpiration,
            lockDuration);
    }

    private static T? MergeSetting<T>(T? existingValue, T? newValue, string queueName, string settingName) where T : struct
    {
        if (existingValue is null) return newValue;

        if (newValue is null) return existingValue;

        if (EqualityComparer<T>.Default.Equals(existingValue.Value, newValue.Value)) return existingValue;

        throw new InvalidOperationException($"Conflicting queue setting '{settingName}' for queue '{queueName}'.");
    }
}