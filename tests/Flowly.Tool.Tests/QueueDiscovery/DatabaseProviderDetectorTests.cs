using Flowly.Tool.QueueDiscovery;

namespace Flowly.Tool.Tests.QueueDiscovery;

public class DatabaseProviderDetectorTests
{
    public class Detect
    {
        [Fact]
        public void WithJobsSqlServerDll_ReturnsSqlServer()
        {
            var source = CreateSourceWithSiblingDlls("Flowly.Jobs.SqlServer.dll");

            var result = DatabaseProviderDetector.Detect([source]);

            Assert.Contains("SqlServer", result);
        }

        [Fact]
        public void WithDeadLettersPostgresDll_ReturnsPostgres()
        {
            var source = CreateSourceWithSiblingDlls("Flowly.DeadLetters.Postgres.dll");

            var result = DatabaseProviderDetector.Detect([source]);

            Assert.Contains("Postgres", result);
        }

        [Fact]
        public void WithMessageStreamsSqlServerDll_ReturnsSqlServer()
        {
            var source = CreateSourceWithSiblingDlls("Flowly.MessageStreams.SqlServer.dll");

            var result = DatabaseProviderDetector.Detect([source]);

            Assert.Contains("SqlServer", result);
        }

        [Fact]
        public void WithMessageStreamsPostgresDll_ReturnsPostgres()
        {
            var source = CreateSourceWithSiblingDlls("Flowly.MessageStreams.Postgres.dll");

            var result = DatabaseProviderDetector.Detect([source]);

            Assert.Contains("Postgres", result);
        }

        [Fact]
        public void WithNoMatchingDlls_ReturnsEmpty()
        {
            var source = CreateSourceWithSiblingDlls("Flowly.RabbitMQ.dll");

            var result = DatabaseProviderDetector.Detect([source]);

            Assert.Empty(result);
        }

        private static QueueDiscoverySource CreateSourceWithSiblingDlls(params string[] siblingDllNames)
        {
            var directory = Directory.CreateTempSubdirectory();

            foreach (var siblingDllName in siblingDllNames)
            {
                File.WriteAllBytes(Path.Combine(directory.FullName, siblingDllName), []);
            }

            var assembly = new FileInfo(Path.Combine(directory.FullName, "MyProject.dll"));

            return new QueueDiscoverySource(assembly, new DirectoryInfo(directory.FullName));
        }
    }
}
