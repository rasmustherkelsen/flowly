using Flowly.Tool.QueueDiscovery;

namespace Flowly.Tool.Tests.QueueDiscovery;

public class QueueDiscoverySourceTests
{
    public class Construction
    {
        [Fact]
        public void PopulatesAssemblyAndDefaultWorkingDirectory()
        {
            var assembly = new FileInfo("/tmp/some.dll");
            var defaultWorkingDirectory = new DirectoryInfo("/tmp");

            var queueDiscoverySource = new QueueDiscoverySource(assembly, defaultWorkingDirectory);

            Assert.Same(assembly, queueDiscoverySource.Assembly);
            Assert.Same(defaultWorkingDirectory, queueDiscoverySource.DefaultWorkingDirectory);
        }
    }

    public class Equality
    {
        [Fact]
        public void TwoInstancesWithSameInputs_AreEqual()
        {
            var assembly = new FileInfo("/tmp/some.dll");
            var workingDirectory = new DirectoryInfo("/tmp");

            var first = new QueueDiscoverySource(assembly, workingDirectory);
            var second = new QueueDiscoverySource(assembly, workingDirectory);

            Assert.Equal(first, second);
        }
    }
}
