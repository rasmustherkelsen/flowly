namespace Flowly.Tool;

internal static class CommandExecutor
{
    public static int ExecuteSafely(Action action)
    {
        try
        {
            action();
            return 0;
        }
        catch (Exception ex)
        {
            WriteError(ex);
            return 1;
        }
    }

    public static async Task<int> ExecuteSafelyAsync(Func<Task> action)
    {
        try
        {
            await action();
            return 0;
        }
        catch (Exception ex)
        {
            WriteError(ex);
            return 1;
        }
    }

    private static void WriteError(Exception ex)
    {
        var prevColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Error: {ex.Message}");
        Console.ForegroundColor = prevColor;
    }
}
