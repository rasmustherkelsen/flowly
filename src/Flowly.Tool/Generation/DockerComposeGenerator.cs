using System.Text;

namespace Flowly.Tool.Generation;

internal static class DockerComposeGenerator
{
    private const string AzureServiceBus = "AzureServiceBus";
    private const string RabbitMq = "RabbitMQ";
    private const string SqlServer = "SqlServer";
    private const string Postgres = "Postgres";

    public static string Generate(
        IReadOnlyCollection<string> transportTypes,
        IReadOnlyCollection<string> databaseProviders,
        string sbconfigRelativePath = "./sbconfig.json")
    {
        var hasAsb = transportTypes.Contains(AzureServiceBus, StringComparer.OrdinalIgnoreCase);
        var hasRabbitMq = transportTypes.Contains(RabbitMq, StringComparer.OrdinalIgnoreCase);
        var hasSqlServer = databaseProviders.Contains(SqlServer, StringComparer.OrdinalIgnoreCase);
        var hasPostgres = databaseProviders.Contains(Postgres, StringComparer.OrdinalIgnoreCase);

        var rabbitMqHostPort = hasAsb && hasRabbitMq ? 5673 : 5672;
        var includeSqlServer = hasAsb || (hasSqlServer && !hasAsb);
        var includePostgres = hasPostgres;

        var sb = new StringBuilder();
        sb.AppendLine("services:");

        if (hasAsb)
        {
            AppendSqlServerService(sb);
            sb.AppendLine();
            AppendAsbEmulatorService(sb, sbconfigRelativePath);
        }
        else if (hasSqlServer)
        {
            AppendSqlServerService(sb);
        }

        if (hasRabbitMq)
        {
            if (hasAsb || hasSqlServer)
            {
                sb.AppendLine();
            }

            AppendRabbitMqService(sb, rabbitMqHostPort);
        }

        if (includePostgres)
        {
            if (hasAsb || hasRabbitMq || hasSqlServer)
            {
                sb.AppendLine();
            }

            AppendPostgresService(sb);
        }

        return sb.ToString();
    }

    private static void AppendSqlServerService(StringBuilder sb)
    {
        sb.AppendLine("  sql-server:");
        sb.AppendLine("    image: mcr.microsoft.com/mssql/server:2022-latest");
        sb.AppendLine("    container_name: servicebus-sql");
        sb.AppendLine("    ports:");
        sb.AppendLine("      - \"1433:1433\"");
        sb.AppendLine("    environment:");
        sb.AppendLine("      - ACCEPT_EULA=Y");
        sb.AppendLine("      - SA_PASSWORD=Pass@word1");
    }

    private static void AppendAsbEmulatorService(StringBuilder sb, string sbconfigRelativePath)
    {
        sb.AppendLine("  servicebus-emulator:");
        sb.AppendLine("    image: mcr.microsoft.com/azure-messaging/servicebus-emulator:latest");
        sb.AppendLine("    environment:");
        sb.AppendLine("      SQL_SERVER: sql-server");
        sb.AppendLine("      MSSQL_SA_PASSWORD: 'Pass@word1'");
        sb.AppendLine("      ACCEPT_EULA: Y");
        sb.AppendLine("      SQL_WAIT_INTERVAL: 15");
        sb.AppendLine("    ports:");
        sb.AppendLine("      - \"5672:5672\"");
        sb.AppendLine("    volumes:");
        sb.AppendLine($"      - {sbconfigRelativePath}:/ServiceBus_Emulator/ConfigFiles/Config.json");
        sb.AppendLine("    depends_on:");
        sb.AppendLine("      - sql-server");
    }

    private static void AppendRabbitMqService(StringBuilder sb, int hostPort)
    {
        sb.AppendLine("  rabbitmq:");
        sb.AppendLine("    image: rabbitmq:3-management");
        sb.AppendLine("    container_name: rabbitmq");
        sb.AppendLine("    ports:");
        sb.AppendLine($"      - \"{hostPort}:5672\"");
        sb.AppendLine("      - \"15672:15672\"");
        sb.AppendLine("    environment:");
        sb.AppendLine("      RABBITMQ_DEFAULT_USER: guest");
        sb.AppendLine("      RABBITMQ_DEFAULT_PASS: guest");
    }

    private static void AppendPostgresService(StringBuilder sb)
    {
        sb.AppendLine("  postgres:");
        sb.AppendLine("    image: postgres:16");
        sb.AppendLine("    container_name: postgres");
        sb.AppendLine("    ports:");
        sb.AppendLine("      - \"5432:5432\"");
        sb.AppendLine("    environment:");
        sb.AppendLine("      POSTGRES_DB: flowly");
        sb.AppendLine("      POSTGRES_USER: postgres");
        sb.AppendLine("      POSTGRES_PASSWORD: postgres");
    }
}
