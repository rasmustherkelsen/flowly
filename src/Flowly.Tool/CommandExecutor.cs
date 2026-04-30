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
            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.ForegroundColor = prevColor;
            return 1;
        }
    }
}
