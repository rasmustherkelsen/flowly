using Flowly;
using Flowly.AzureServiceBus;
using Flowly.DeadLetters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = false,
    flowlyBuilder => flowlyBuilder
        .UseAzureServiceBus()
        .AddSqlServerDeadLetterTracking("FlowlyDeadLetters", false));

var host = builder.Build();

await using var scope = host.Services.CreateAsyncScope();
var deadLetterService = scope.ServiceProvider.GetRequiredService<IDeadLetterService>();

List<IDeadLetter>? currentList = null;

PrintHelp();

while (true)
{
    Console.Write("\n> ");
    var input = Console.ReadLine()?.Trim();

    if (input is null or "q" or "quit")
        break;

    if (input is "l" or "list")
    {
        currentList = [.. await deadLetterService.GetDeadLetters()];
        PrintTable(currentList);
        continue;
    }

    if (input is "?" or "help")
    {
        PrintHelp();
        continue;
    }

    var parts = input.Split(' ', 2);
    if (parts.Length == 2 && int.TryParse(parts[1], out var index))
    {
        if (currentList is null)
        {
            Console.WriteLine("Run 'list' first to load dead letters.");
            continue;
        }

        if (index < 1 || index > currentList.Count)
        {
            Console.WriteLine($"Invalid number. List has {currentList.Count} item(s). Run 'list' to refresh.");
            continue;
        }

        var messageId = currentList[index - 1].MessageId;

        try
        {
            if (parts[0] is "r" or "requeue")
            {
                await deadLetterService.Requeue(messageId);
                Console.WriteLine($"#{index} requeued.");
                currentList = null;
            }
            else if (parts[0] is "d" or "discard")
            {
                await deadLetterService.Discard(messageId);
                Console.WriteLine($"#{index} discarded.");
                currentList = null;
            }
            else
            {
                Console.WriteLine($"Unknown command '{parts[0]}'. Type 'help' for available commands.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        continue;
    }

    Console.WriteLine("Unknown command. Type 'help' for available commands.");
}

static void PrintHelp()
{
    Console.WriteLine();
    Console.WriteLine("Dead Letter Manager");
    Console.WriteLine("===================");
    Console.WriteLine("  list (l)         — load and display all dead letters");
    Console.WriteLine("  requeue <#> (r)  — requeue item by number from the list");
    Console.WriteLine("  discard <#> (d)  — discard item by number from the list");
    Console.WriteLine("  help (?)         — show this help");
    Console.WriteLine("  quit (q)         — exit");
}

static void PrintTable(List<IDeadLetter> deadLetters)
{
    if (deadLetters.Count == 0)
    {
        Console.WriteLine("No dead letters found.");
        return;
    }

    const int colNum = 4;
    const int colStatus = 10;
    const int colQueue = 32;
    const int colReason = 48;
    const int colDate = 19;

    Console.WriteLine();
    Console.WriteLine(
        $"{"#".PadRight(colNum)}" +
        $"{"Status".PadRight(colStatus)}" +
        $"{"Queue".PadRight(colQueue)}" +
        $"{"Reason".PadRight(colReason)}" +
        $"{"Dead-lettered at".PadRight(colDate)}");

    Console.WriteLine(new string('-', colNum + colStatus + colQueue + colReason + colDate));

    for (var i = 0; i < deadLetters.Count; i++)
    {
        var dl = deadLetters[i];
        var date = dl.DeadLetteredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        Console.WriteLine(
            $"{(i + 1).ToString().PadRight(colNum)}" +
            $"{dl.Status.ToString().PadRight(colStatus)}" +
            $"{Truncate(dl.QueueName, colQueue).PadRight(colQueue)}" +
            $"{Truncate(dl.DeadLetterReason ?? "-", colReason).PadRight(colReason)}" +
            $"{date.PadRight(colDate)}");
    }

    Console.WriteLine();
    Console.WriteLine($"{deadLetters.Count} dead letter(s). Run 'list' again to refresh.");
}

static string Truncate(string value, int maxLength)
{
    return value.Length <= maxLength ? value : value[..(maxLength - 2)] + "..";
}
