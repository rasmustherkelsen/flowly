using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.BackgroundServices;

internal class FakeReceivedMessage<TMessage>(TMessage body) : IReceivedMessage<TMessage>
{
    public TMessage Body { get; } = body;
    public MessageProperties Properties { get; } = MessageProperties.Empty;
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
