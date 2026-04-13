using Flowly.MessageInfrastructure.Registration;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class ProviderQueueManifestTests
{
    public class Add
    {
        [Fact]
        public void NewQueue_IsAddedToManifest()
        {
            var manifest = new ProviderQueueManifest("primary", isPrimary: true, "AzureServiceBus");
            var registration = new DeferredQueueRegistration("my-queue");

            manifest.Add(registration);

            Assert.Single(manifest.Queues);
            Assert.Equal("my-queue", manifest.Queues[0].QueueName);
        }

        [Fact]
        public void BlankQueueName_IsIgnored()
        {
            var manifest = new ProviderQueueManifest("primary", isPrimary: true, "AzureServiceBus");

            manifest.Add(new DeferredQueueRegistration(""));
            manifest.Add(new DeferredQueueRegistration("   "));

            Assert.Empty(manifest.Queues);
        }

        [Fact]
        public void DuplicateQueueName_MergesRequiresSession()
        {
            var manifest = new ProviderQueueManifest("primary", isPrimary: true, "AzureServiceBus");
            manifest.Add(new DeferredQueueRegistration("my-queue", RequiresSession: false));
            manifest.Add(new DeferredQueueRegistration("my-queue", RequiresSession: true));

            Assert.Single(manifest.Queues);
            Assert.True(manifest.Queues[0].RequiresSession);
        }

        [Fact]
        public void DuplicateQueueNameWithSameSettings_MergesWithoutError()
        {
            var ttl = TimeSpan.FromDays(2);
            var manifest = new ProviderQueueManifest("primary", isPrimary: true, "AzureServiceBus");
            manifest.Add(new DeferredQueueRegistration("my-queue", DefaultMessageTimeToLive: ttl));
            manifest.Add(new DeferredQueueRegistration("my-queue", DefaultMessageTimeToLive: ttl));

            Assert.Single(manifest.Queues);
            Assert.Equal(ttl, manifest.Queues[0].DefaultMessageTimeToLive);
        }

        [Fact]
        public void DuplicateQueueNameWithConflictingTtl_ThrowsInvalidOperationException()
        {
            var manifest = new ProviderQueueManifest("primary", isPrimary: true, "AzureServiceBus");
            manifest.Add(new DeferredQueueRegistration("my-queue", DefaultMessageTimeToLive: TimeSpan.FromDays(1)));

            Assert.Throws<InvalidOperationException>(() =>
                manifest.Add(new DeferredQueueRegistration("my-queue", DefaultMessageTimeToLive: TimeSpan.FromDays(2))));
        }

        [Fact]
        public void DuplicateQueueNameWithConflictingLockDuration_ThrowsInvalidOperationException()
        {
            var manifest = new ProviderQueueManifest("primary", isPrimary: true, "AzureServiceBus");
            manifest.Add(new DeferredQueueRegistration("my-queue", LockDuration: TimeSpan.FromMinutes(5)));

            Assert.Throws<InvalidOperationException>(() =>
                manifest.Add(new DeferredQueueRegistration("my-queue", LockDuration: TimeSpan.FromMinutes(10))));
        }

        [Fact]
        public void DuplicateQueueNameWithConflictingDeadLetterSetting_ThrowsInvalidOperationException()
        {
            var manifest = new ProviderQueueManifest("primary", isPrimary: true, "AzureServiceBus");
            manifest.Add(new DeferredQueueRegistration("my-queue", DeadLetterOnMessageExpiration: true));

            Assert.Throws<InvalidOperationException>(() =>
                manifest.Add(new DeferredQueueRegistration("my-queue", DeadLetterOnMessageExpiration: false)));
        }

        [Fact]
        public void QueueNameLookup_IsCaseInsensitive()
        {
            var manifest = new ProviderQueueManifest("primary", isPrimary: true, "AzureServiceBus");
            manifest.Add(new DeferredQueueRegistration("My-Queue"));
            manifest.Add(new DeferredQueueRegistration("my-queue"));

            Assert.Single(manifest.Queues);
        }

        [Fact]
        public void NullValueSettings_MergeWithConcreteValues()
        {
            var ttl = TimeSpan.FromDays(3);
            var manifest = new ProviderQueueManifest("primary", isPrimary: true, "AzureServiceBus");
            manifest.Add(new DeferredQueueRegistration("my-queue", DefaultMessageTimeToLive: null));
            manifest.Add(new DeferredQueueRegistration("my-queue", DefaultMessageTimeToLive: ttl));

            Assert.Equal(ttl, manifest.Queues[0].DefaultMessageTimeToLive);
        }
    }
}
