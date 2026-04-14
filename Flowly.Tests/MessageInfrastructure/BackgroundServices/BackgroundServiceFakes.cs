using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.BackgroundServices;

internal class FakeMessageBusClientRegistry(IMessageBusClient client) : IMessageBusClientRegistry
{
    public string PrimaryProviderName => "azure-service-bus";

    public IMessageBusClient GetClient(string providerName) => client;

    public bool IsRegistered(string providerName) => true;

    public IReadOnlyList<RegisteredTransport> GetAll() => [new RegisteredTransport("azure-service-bus", IsPrimary: true, CreateTopologyOverride: null)];

    public void Register(string providerName, IMessageBusClient messageBusClient, bool? createTopologyOverride) { }
}

internal class FakeReceivedMessage<TMessage>(TMessage body, MessageProperties? properties = null) : IReceivedMessage<TMessage>
{
    public TMessage Body { get; } = body;
    public MessageProperties Properties { get; } = properties ?? MessageProperties.Empty;
    public bool Completed { get; private set; }
    public bool DeadLettered { get; private set; }
    public string? DeadLetterReason { get; private set; }

    public Task Complete(CancellationToken cancellationToken = default)
    {
        Completed = true;
        return Task.CompletedTask;
    }

    public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
    {
        DeadLettered = true;
        DeadLetterReason = reason;
        return Task.CompletedTask;
    }
}

internal class ThrowingReceivedMessage<TMessage> : IReceivedMessage<TMessage>
{
    public TMessage Body => throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TMessage).FullName}.");
    public MessageProperties Properties { get; } = MessageProperties.Empty;
    public bool DeadLettered { get; private set; }
    public string? DeadLetterReason { get; private set; }

    public Task Complete(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
    {
        DeadLettered = true;
        DeadLetterReason = reason;
        return Task.CompletedTask;
    }
}

internal class FakeServiceScopeFactory<TService>(TService service) : IServiceScopeFactory
{
    public int ScopesCreated { get; private set; }

    public IServiceScope CreateScope()
    {
        ScopesCreated++;
        return new FakeScope(service);
    }

    private class FakeScope(TService service) : IServiceScope, IAsyncDisposable
    {
        public IServiceProvider ServiceProvider { get; } = new FakeProvider(service);
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private class FakeProvider(TService service) : IServiceProvider
        {
            public object? GetService(Type serviceType) => serviceType == typeof(TService) ? service : null;
        }
    }
}
