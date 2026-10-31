using Flowly.MessageInfrastructure.StreamPartitions;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.StreamPartitions;

public class StreamPartitionResumeRegistryTests
{
    private static StreamPartitionKey Key(int? partition) => new("orders", "OrderConsumer", partition);

    public class Resume
    {
        [Fact]
        public void WhenNothingIsRegistered_ReturnsNotOwned()
        {
            var streamPartitionResumeRegistry = new StreamPartitionResumeRegistry();

            Assert.Equal(StreamPartitionResumeResult.NotOwned, streamPartitionResumeRegistry.Resume(Key(0)));
        }

        [Fact]
        public void WhenThePartitionIsHalted_ResumesItAndReturnsResumed()
        {
            var streamPartitionResumeRegistry = new StreamPartitionResumeRegistry();
            var partition = new FakeResumablePartition(true);
            streamPartitionResumeRegistry.Register(Key(0), partition);

            var result = streamPartitionResumeRegistry.Resume(Key(0));

            Assert.Equal(StreamPartitionResumeResult.Resumed, result);
            Assert.Equal(1, partition.ResumeCalls);
        }

        [Fact]
        public void WhenThePartitionIsNotHalted_ReturnsNotHalted()
        {
            var streamPartitionResumeRegistry = new StreamPartitionResumeRegistry();
            streamPartitionResumeRegistry.Register(Key(0), new FakeResumablePartition(false));

            Assert.Equal(StreamPartitionResumeResult.NotHalted, streamPartitionResumeRegistry.Resume(Key(0)));
        }

        [Fact]
        public void OnlyResumesTheAddressedPartition()
        {
            var streamPartitionResumeRegistry = new StreamPartitionResumeRegistry();
            var other = new FakeResumablePartition(true);
            streamPartitionResumeRegistry.Register(Key(0), new FakeResumablePartition(true));
            streamPartitionResumeRegistry.Register(Key(1), other);

            streamPartitionResumeRegistry.Resume(Key(0));

            Assert.Equal(0, other.ResumeCalls);
        }

        [Fact]
        public void ForANonPartitionedStream_AddressesTheNullPartition()
        {
            var streamPartitionResumeRegistry = new StreamPartitionResumeRegistry();
            streamPartitionResumeRegistry.Register(Key(null), new FakeResumablePartition(true));

            Assert.Equal(StreamPartitionResumeResult.Resumed, streamPartitionResumeRegistry.Resume(Key(null)));
            Assert.Equal(StreamPartitionResumeResult.NotOwned, streamPartitionResumeRegistry.Resume(Key(0)));
        }

        [Fact]
        public void DistinguishesConsumersOfTheSameQueue()
        {
            var streamPartitionResumeRegistry = new StreamPartitionResumeRegistry();
            streamPartitionResumeRegistry.Register(Key(0), new FakeResumablePartition(true));

            Assert.Equal(StreamPartitionResumeResult.NotOwned, streamPartitionResumeRegistry.Resume(new StreamPartitionKey("orders", "OtherConsumer", 0)));
        }
    }

    public class Register
    {
        [Fact]
        public void WhenTheRegistrationIsDisposed_RemovesThePartition()
        {
            var streamPartitionResumeRegistry = new StreamPartitionResumeRegistry();
            var registration = streamPartitionResumeRegistry.Register(Key(0), new FakeResumablePartition(true));

            registration.Dispose();

            Assert.Equal(StreamPartitionResumeResult.NotOwned, streamPartitionResumeRegistry.Resume(Key(0)));
        }

        [Fact]
        public void WhenAnotherLoopReplacedTheRegistration_DisposingTheOldOneKeepsTheNewOne()
        {
            var streamPartitionResumeRegistry = new StreamPartitionResumeRegistry();
            var oldRegistration = streamPartitionResumeRegistry.Register(Key(0), new FakeResumablePartition(true));
            var replacement = new FakeResumablePartition(true);
            streamPartitionResumeRegistry.Register(Key(0), replacement);

            oldRegistration.Dispose();
            streamPartitionResumeRegistry.Resume(Key(0));

            Assert.Equal(1, replacement.ResumeCalls);
        }
    }

    public class GetOrCreate
    {
        [Fact]
        public void RegistersTheRegistryAsRegistryAndResumer()
        {
            var services = new ServiceCollection();

            var streamPartitionResumeRegistry = StreamPartitionResumeRegistry.GetOrCreate(services);

            using var serviceProvider = services.BuildServiceProvider();
            Assert.Same(streamPartitionResumeRegistry, serviceProvider.GetRequiredService<IStreamPartitionResumeRegistry>());
            Assert.Same(streamPartitionResumeRegistry, serviceProvider.GetRequiredService<IStreamPartitionResumer>());
        }

        [Fact]
        public void CalledTwice_ReturnsTheSameRegistryAndRegistersItOnce()
        {
            var services = new ServiceCollection();

            var first = StreamPartitionResumeRegistry.GetOrCreate(services);
            var second = StreamPartitionResumeRegistry.GetOrCreate(services);

            Assert.Same(first, second);
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IStreamPartitionResumeRegistry));
        }
    }

    private sealed class FakeResumablePartition(bool halted) : IResumableStreamPartition
    {
        public int ResumeCalls { get; private set; }

        public bool Resume()
        {
            ResumeCalls++;
            return halted;
        }
    }
}
