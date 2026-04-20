namespace Flowly.Tool;

internal static class OutputWriter
{
    public static void Write(string content, FileInfo? output)
    {
        if (output is null)
        {
            Console.WriteLine(content);
            return;
        }

        var outputPath = Path.GetFullPath(output.FullName);
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputPath, content);
        Console.WriteLine($"Wrote output to: {outputPath}");
    }
}
