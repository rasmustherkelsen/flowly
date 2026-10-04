using System.CommandLine;
using Flowly.MessageStreams;
using Flowly.MessageStreams.DatabaseModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Flowly.Tool.Commands;

internal static class MessageStreamCommand
{
    public static Command Build()
    {
        var command = new Command("message-stream", "Operate on Flowly message stream checkpoint persistence");
        command.Add(BuildListCheckpointsCommand());
        command.Add(BuildResetCheckpointCommand());
        return command;
    }

    private static Option<string> CreateProviderOption() =>
        new("--provider") { Description = "The checkpoint persistence provider: sqlserver, postgres, or sqlite", Required = true };

    private static Option<string> CreateConnectionOption() =>
        new("--connection") { Description = "The connection string for the checkpoint database", Required = true };

    private static Command BuildListCheckpointsCommand()
    {
        var providerOption = CreateProviderOption();
        var connectionOption = CreateConnectionOption();
        var queueOption = new Option<string?>("--queue") { Description = "Filter by queue name" };
        var consumerOption = new Option<string?>("--consumer") { Description = "Filter by consumer name" };

        var command = new Command("list-checkpoints", "List stored message stream checkpoint positions");
        command.Add(providerOption);
        command.Add(connectionOption);
        command.Add(queueOption);
        command.Add(consumerOption);
        command.SetAction((parseResult, cancellationToken) => CommandExecutor.ExecuteSafelyAsync(async () =>
        {
            var provider = parseResult.GetValue(providerOption)!;
            var connection = parseResult.GetValue(connectionOption)!;
            var queue = parseResult.GetValue(queueOption);
            var consumer = parseResult.GetValue(consumerOption);

            var dbContextFactory = CreateDbContextFactory(provider, connection);
            var checkpoints = await MessageStreamCheckpointAdmin.List(dbContextFactory, queue, consumer, cancellationToken);

            if (checkpoints.Count == 0)
            {
                Console.WriteLine("No checkpoints stored.");
                return;
            }

            Console.WriteLine($"{"QUEUE",-30} {"CONSUMER",-30} {"PARTITION",-10} {"POSITION",-12} UPDATED (UTC)");
            foreach (var checkpoint in checkpoints)
            {
                var partition = checkpoint.Partition?.ToString() ?? "-";
                var position = checkpoint.Position?.ToString() ?? "(none)";
                Console.WriteLine($"{checkpoint.QueueName,-30} {checkpoint.ConsumerName,-30} {partition,-10} {position,-12} {checkpoint.UpdatedAtUtc:O}");
            }
        }));

        return command;
    }

    private static Command BuildResetCheckpointCommand()
    {
        var providerOption = CreateProviderOption();
        var connectionOption = CreateConnectionOption();
        var queueOption = new Option<string>("--queue") { Description = "The queue name of the stream to reset", Required = true };
        var consumerOption = new Option<string>("--consumer") { Description = "The consumer name to reset", Required = true };
        var partitionOption = new Option<int?>("--partition") { Description = "The partition to reset. Omit for a non-partitioned stream" };
        var offsetOption = new Option<long>("--offset") { Description = "The offset to reset the position to", Required = true };
        var yesOption = new Option<bool>("--yes") { Description = "Confirm the reset without an interactive prompt" };

        var command = new Command(
            "reset-checkpoint",
            "Reset a message stream's checkpoint position. The running handler only picks this up on its next restart, " +
            "and resetting to an earlier offset causes those messages to be reprocessed.");
        command.Add(providerOption);
        command.Add(connectionOption);
        command.Add(queueOption);
        command.Add(consumerOption);
        command.Add(partitionOption);
        command.Add(offsetOption);
        command.Add(yesOption);
        command.SetAction((parseResult, cancellationToken) => CommandExecutor.ExecuteSafelyAsync(async () =>
        {
            var provider = parseResult.GetValue(providerOption)!;
            var connection = parseResult.GetValue(connectionOption)!;
            var queue = parseResult.GetValue(queueOption)!;
            var consumer = parseResult.GetValue(consumerOption)!;
            var partition = parseResult.GetValue(partitionOption);
            var offset = parseResult.GetValue(offsetOption);
            var confirmed = parseResult.GetValue(yesOption);

            if (!confirmed)
            {
                Console.Write($"Reset checkpoint for queue '{queue}', consumer '{consumer}'" +
                    $"{(partition.HasValue ? $", partition {partition}" : "")} to offset {offset}? " +
                    "This causes reprocessing of any messages after the new offset once the handler restarts. [y/N] ");
                var response = Console.ReadLine();
                if (!string.Equals(response?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Aborted.");
                    return;
                }
            }

            var dbContextFactory = CreateDbContextFactory(provider, connection);
            await MessageStreamCheckpointAdmin.Reset(dbContextFactory, queue, consumer, partition, offset, cancellationToken);

            Console.WriteLine($"Checkpoint for queue '{queue}', consumer '{consumer}' reset to offset {offset}.");
        }));

        return command;
    }

    private static IDbContextFactory<MessageStreamCheckpointDataContext> CreateDbContextFactory(string provider, string connection)
    {
        var builder = new DbContextOptionsBuilder<MessageStreamCheckpointDataContext>();

        switch (provider.ToLowerInvariant())
        {
            case "sqlserver":
                builder.UseSqlServer(connection);
                break;
            case "postgres":
                builder.UseNpgsql(connection);
                break;
            case "sqlite":
                builder.UseSqlite(connection);
                break;
            default:
                throw new InvalidOperationException($"Unknown --provider '{provider}'. Expected sqlserver, postgres, or sqlite.");
        }

        return new PooledDbContextFactory<MessageStreamCheckpointDataContext>(builder.Options);
    }
}
