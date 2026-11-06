using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Flowly.DeadLetters.Tests.Fakes;

internal sealed class RecordingCommandInterceptor : DbCommandInterceptor
{
    private readonly List<string> _commandTexts = [];

    public IReadOnlyList<string> SelectCommandTexts
    {
        get
        {
            lock (_commandTexts)
            {
                return _commandTexts.Where(commandText => commandText.StartsWith("SELECT", StringComparison.Ordinal)).ToList();
            }
        }
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        lock (_commandTexts)
        {
            _commandTexts.Add(command.CommandText);
        }

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
