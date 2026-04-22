using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.BackgroundServices;

internal class FakeMessageBusClientRegistry(IMessageBusClient client) : IMessageBusClientRegistry
{
    public string PrimaryProviderName => "azure-service-bus";

    public IMessageBusClient GetClient(string providerName)
    {
        return client;
    }

    public bool IsRegistered(string providerName)
    {
        return true;
    }

    public IReadOnlyList<RegisteredTransport> GetAll()
    {
        return [new RegisteredTransport("azure-service-bus", true, null)];
    }

    public void Register(string providerName, IMessageBusClient messageBusClient, bool? createTopologyOverride)
    {
    }
}

internal class FakeReceivedMessage<TMessage>(TMessage body, MessageProperties? properties = null) : IReceivedMessage<TMessage>
{
    public bool Completed { get; private set; }
    public bool DeadLettered { get; private set; }
    public string? DeadLetterReason { get; private set; }
    public TMessage Body { get; } = body;
    public MessageProperties Properties { get; } = properties ?? MessageProperties.Empty;

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
    public bool DeadLettered { get; private set; }
    public string? DeadLetterReason { get; private set; }
    public TMessage Body => throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TMessage).FullName}.");
    public MessageProperties Properties { get; } = MessageProperties.Empty;

    public Task Complete(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

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
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public IServiceProvider ServiceProvider { get; } = new FakeProvider(service);

        public void Dispose()
        {
        }

        private class FakeProvider(TService service) : IServiceProvider
        {
            public object? GetService(Type serviceType)
            {
                return serviceType == typeof(TService) ? service : null;
            }
        }
    }
}