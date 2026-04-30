namespace Flowly.Tool.QueueDiscovery;

internal static class DatabaseProviderDetector
{
    private static readonly string[] SqlServerDlls =
    [
        "Flowly.Jobs.SqlServer.dll",
        "Flowly.DeadLetters.SqlServer.dll"
    ];

    private static readonly string[] PostgresDlls =
    [
        "Flowly.Jobs.Postgres.dll",
        "Flowly.DeadLetters.Postgres.dll"
    ];

    public static IReadOnlySet<string> Detect(IReadOnlyList<QueueDiscoverySource> sources)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            var dir = Path.GetDirectoryName(source.Assembly.FullName)!;

            if (SqlServerDlls.Any(dll => File.Exists(Path.Combine(dir, dll))))
            {
                result.Add("SqlServer");
            }

            if (PostgresDlls.Any(dll => File.Exists(Path.Combine(dir, dll))))
            {
                result.Add("Postgres");
            }
        }

        return result;
    }
}
