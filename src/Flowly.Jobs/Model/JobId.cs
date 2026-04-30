namespace Flowly.Jobs.Model;

/// <summary>
///     A strongly-typed identifier for a job instance, returned by
///     <see cref="IJobMessageSender.QueueJob{TMessage}" />. Wraps a <see cref="Guid" /> to prevent accidental
///     confusion with other identifiers in the application.
/// </summary>
public record JobId
{
    /// <summary>
    ///     Initialises a new <see cref="JobId" /> with a freshly generated <see cref="Guid" />.
    /// </summary>
    public JobId()
    {
        InnerId = Guid.NewGuid();
    }

    /// <summary>
    ///     Initialises a <see cref="JobId" /> wrapping an existing <see cref="Guid" />.
    /// </summary>
    /// <param name="innerId">The <see cref="Guid" /> to wrap.</param>
    public JobId(Guid innerId)
    {
        InnerId = innerId;
    }

    /// <summary>
    ///     The underlying <see cref="Guid" /> that uniquely identifies the job instance in the tracking database.
    /// </summary>
    public Guid InnerId { get; init; }

    /// <summary>
    ///     Returns the string representation of <see cref="InnerId" />.
    /// </summary>
    /// <returns>The job identifier formatted as a GUID string.</returns>
    public override string ToString()
    {
        return InnerId.ToString();
    }
}