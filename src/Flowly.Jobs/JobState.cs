using System.Text.Json.Serialization;

namespace Flowly.Jobs;

/// <summary>
///     The lifecycle state of a tracked job, stored in the <c>Job</c> database table and surfaced via
///     <see cref="JobInfo.CurrentState" />.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum JobState
{
    /// <summary>
    ///     The job has been enqueued but not yet picked up by a handler.
    /// </summary>
    Created,

    /// <summary>
    ///     A handler has begun processing the job.
    /// </summary>
    Started,

    /// <summary>
    ///     The handler finished processing the job successfully.
    /// </summary>
    Completed,

    /// <summary>
    ///     All retry attempts have been exhausted and the job could not be completed.
    /// </summary>
    Failed
}