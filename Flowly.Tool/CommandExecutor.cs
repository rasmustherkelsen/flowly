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
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
