namespace Flowly.Jobs;

/// <summary>
///     Context passed to the job handler when processing a job message. Provides access to the message, a cancellation
///     token, and a method to save state related to the job.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IJobMessageContext<T>
{
    /// <summary>
    ///     The job message being processed. This property provides access to the message that triggered the job, allowing the
    ///     handler to read any necessary information from it to perform the required processing.
    /// </summary>
    T Message { get; }

    /// <summary>
    ///     A cancellation token that can be used to signal cancellation of the job processing. This allows the job handler to
    ///     gracefully stop processing if a cancellation request is made, ensuring that any necessary cleanup can be performed
    ///     and resources can be released appropriately.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    ///     Saves state related to the job. This can be used to persist any relevant information that may be needed for future
    ///     processing or auditing purposes.
    /// </summary>
    /// <param name="state">
    ///     The state to be saved. This can include any data necessary for the job's execution or for auditing
    ///     purposes.
    /// </param>
    /// <typeparam name="TState">The type of the state object to be saved.</typeparam>
    /// <returns>A task representing the asynchronous operation of saving the state.</returns>
    Task SaveState<TState>(TState state) where TState : class;
}