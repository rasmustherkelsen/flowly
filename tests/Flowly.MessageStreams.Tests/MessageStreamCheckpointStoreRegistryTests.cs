using Flowly.MessageStreams.DatabaseModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Flowly.MessageStreams.Tests;

public class MessageStreamCheckpointStoreRegistryTests
{
    public class Register
    {
        [Fact]
        public void SecondUnnamedRegistration_Throws()
        {
            var registry = new MessageStreamCheckpointStoreRegistry();
            registry.Register(null, CreateFactory());

            Assert.Throws<InvalidOperationException>(() => registry.Register(null, CreateFactory()));
        }

        [Fact]
        public void SecondNamedRegistration_DoesNotThrow()
        {
            var registry = new MessageStreamCheckpointStoreRegistry();
            registry.Register(null, CreateFactory());

            registry.Register("secondary", CreateFactory());

            Assert.NotNull(registry.Resolve("secondary"));
        }
    }

    public class Resolve
    {
        [Fact]
        public void WithNoNameAndNoRegistration_Throws()
        {
            var registry = new MessageStreamCheckpointStoreRegistry();

            Assert.Throws<InvalidOperationException>(() => registry.Resolve(null));
        }

        [Fact]
        public void WithNoName_ReturnsDefaultStore()
        {
            var registry = new MessageStreamCheckpointStoreRegistry();
            var factory = CreateFactory();
            registry.Register(null, factory);

            Assert.Same(factory, registry.Resolve(null));
        }

        [Fact]
        public void WithUnknownName_Throws()
        {
            var registry = new MessageStreamCheckpointStoreRegistry();
            registry.Register(null, CreateFactory());

            Assert.Throws<InvalidOperationException>(() => registry.Resolve("does-not-exist"));
        }

        [Fact]
        public void WithMatchingName_ReturnsThatStore()
        {
            var registry = new MessageStreamCheckpointStoreRegistry();
            registry.Register(null, CreateFactory());
            var named = CreateFactory();
            registry.Register("named", named);

            Assert.Same(named, registry.Resolve("named"));
        }
    }

    private static IDbContextFactory<MessageStreamCheckpointDataContext> CreateFactory()
    {
        var builder = new DbContextOptionsBuilder<MessageStreamCheckpointDataContext>();
        builder.UseSqlite("Data Source=:memory:");
        return new PooledDbContextFactory<MessageStreamCheckpointDataContext>(builder.Options);
    }
}
