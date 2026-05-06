using Flowly.MessageInfrastructure.Registration;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class RegisteredTransportTests
{
    public class Constructor
    {
        [Fact]
        public void StoresName()
        {
            var registeredTransport = new RegisteredTransport("azure-service-bus", true, null);

            Assert.Equal("azure-service-bus", registeredTransport.Name);
        }

        [Fact]
        public void StoresIsPrimary()
        {
            var registeredTransport = new RegisteredTransport("rabbitmq", false, null);

            Assert.False(registeredTransport.IsPrimary);
        }

        [Fact]
        public void StoresCreateTopologyOverride()
        {
            var registeredTransport = new RegisteredTransport("azure-service-bus", true, false);

            Assert.False(registeredTransport.CreateTopologyOverride);
        }

        [Fact]
        public void WithNullCreateTopologyOverride_PreservesNull()
        {
            var registeredTransport = new RegisteredTransport("azure-service-bus", true, null);

            Assert.Null(registeredTransport.CreateTopologyOverride);
        }
    }

    public class Equality
    {
        [Fact]
        public void IdenticalRecords_AreEqual()
        {
            var first = new RegisteredTransport("azure-service-bus", true, null);
            var second = new RegisteredTransport("azure-service-bus", true, null);

            Assert.Equal(first, second);
        }

        [Fact]
        public void DifferentPrimaryFlag_NotEqual()
        {
            var first = new RegisteredTransport("azure-service-bus", true, null);
            var second = new RegisteredTransport("azure-service-bus", false, null);

            Assert.NotEqual(first, second);
        }
    }
}
