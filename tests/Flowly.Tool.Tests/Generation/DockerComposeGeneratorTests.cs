using Flowly.Tool.Generation;

namespace Flowly.Tool.Tests.Generation;

public class DockerComposeGeneratorTests
{
    public class Generate
    {
        [Fact]
        public void WithRabbitMqOnly_IncludesSingleRabbitMqServiceOnPort5672()
        {
            var yaml = DockerComposeGenerator.Generate(["RabbitMQ"], []);

            Assert.Contains("rabbitmq:", yaml);
            Assert.Contains("\"5672:5672\"", yaml);
            Assert.DoesNotContain("servicebus-emulator", yaml);
            Assert.DoesNotContain("sql-server", yaml);
        }

        [Fact]
        public void WithAsbOnly_IncludesSqlServerAndEmulator()
        {
            var yaml = DockerComposeGenerator.Generate(["AzureServiceBus"], []);

            Assert.Contains("sql-server:", yaml);
            Assert.Contains("servicebus-emulator:", yaml);
            Assert.DoesNotContain("rabbitmq:", yaml);
        }

        [Fact]
        public void WithAsbOnly_EmulatorMountsSbconfig()
        {
            var yaml = DockerComposeGenerator.Generate(["AzureServiceBus"], []);

            Assert.Contains("./sbconfig.json:/ServiceBus_Emulator/ConfigFiles/Config.json", yaml);
        }

        [Fact]
        public void WithBothAsbAndRabbitMq_RabbitMqUsesPort5673()
        {
            var yaml = DockerComposeGenerator.Generate(["AzureServiceBus", "RabbitMQ"], []);

            Assert.Contains("\"5673:5672\"", yaml);
        }

        [Fact]
        public void WithBothAsbAndRabbitMq_AllThreeServicesPresent()
        {
            var yaml = DockerComposeGenerator.Generate(["AzureServiceBus", "RabbitMQ"], []);

            Assert.Contains("sql-server:", yaml);
            Assert.Contains("servicebus-emulator:", yaml);
            Assert.Contains("rabbitmq:", yaml);
        }

        [Fact]
        public void WithSqlServerAndRabbitMqNoAsb_IncludesBothServices()
        {
            var yaml = DockerComposeGenerator.Generate(["RabbitMQ"], ["SqlServer"]);

            Assert.Contains("sql-server:", yaml);
            Assert.Contains("rabbitmq:", yaml);
            Assert.DoesNotContain("servicebus-emulator", yaml);
        }

        [Fact]
        public void WithSqlServerAndAsb_SqlServerNotDuplicated()
        {
            var yaml = DockerComposeGenerator.Generate(["AzureServiceBus"], ["SqlServer"]);

            var occurrences = CountOccurrences(yaml, "sql-server:");
            Assert.Equal(1, occurrences);
        }

        [Fact]
        public void WithPostgresAndRabbitMq_IncludesBothServices()
        {
            var yaml = DockerComposeGenerator.Generate(["RabbitMQ"], ["Postgres"]);

            Assert.Contains("rabbitmq:", yaml);
            Assert.Contains("postgres:", yaml);
            Assert.DoesNotContain("sql-server:", yaml);
        }

        [Fact]
        public void WithNoTransportsAndNoDb_OutputContainsServicesHeader()
        {
            var yaml = DockerComposeGenerator.Generate([], []);

            Assert.Contains("services:", yaml);
        }

        [Fact]
        public void WithCustomSbconfigPath_UsesProvidedPath()
        {
            var yaml = DockerComposeGenerator.Generate(["AzureServiceBus"], [], "./config/sbconfig.json");

            Assert.Contains("./config/sbconfig.json:/ServiceBus_Emulator/ConfigFiles/Config.json", yaml);
        }

        [Fact]
        public void WithPostgresAndAsb_IncludesSqlServerForEmulatorAndPostgresForApp()
        {
            var yaml = DockerComposeGenerator.Generate(["AzureServiceBus"], ["Postgres"]);

            Assert.Contains("sql-server:", yaml);
            Assert.Contains("servicebus-emulator:", yaml);
            Assert.Contains("postgres:", yaml);
        }

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var index = 0;

            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
    }
}
