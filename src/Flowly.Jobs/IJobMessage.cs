namespace Flowly.Jobs;

/// <summary>
///     Marker interface that all job message contracts must implement. Flowly uses the properties of this interface to
///     populate the initial job state record when a job is queued. Apply this interface to your message class and
///     register a corresponding <see cref="JobHandler{TMessage}" /> using
///     <see cref="JobHandlerRegistrationExtensions.AddJobHandler{TMessage,THandler}" />.
/// </summary>
public interface IJobMessage
{
    /// <summary>
    ///     A human-readable description of this specific job instance, shown in the job tracking UI and stored in the
    ///     <c>Job</c> table. Should describe what this particular execution does (e.g. <c>"Import orders for 2024-01-15"</c>).
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     The logical type name of the job, used to group job instances by kind in the tracking database. Typically
    ///     matches the handler class name (e.g. <c>"ImportOrdersJob"</c>).
    /// </summary>
    public string JobTypeName { get; }
}