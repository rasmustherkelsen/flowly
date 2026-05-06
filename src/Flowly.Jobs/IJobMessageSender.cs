using Flowly.Jobs.Model;

namespace Flowly.Jobs;

/// <summary>
///     Sends job messages to the message broker and returns a <see cref="JobId" /> that can be used to track execution
///     progress via <see cref="IJobTrackingService" />. Resolved from the DI container after calling
///     <see cref="SubmitterRegistrationExtensions.AddJobSubmitter{TMessage}" /> on the Flowly builder.
/// </summary>
public interface IJobMessageSender
{
    /// <summary>
    ///     Enqueues a job message of type <typeparamref name="TMessage" /> and returns the <see cref="JobId" /> assigned
    ///     to the new job instance. The job is created in the <c>Created</c> state in the tracking database and
    ///     transitions to <c>Started</c> when the <see cref="JobHandler{TMessage}" /> picks it up.
    /// </summary>
    /// <param name="message">The job message to enqueue. Must implement <see cref="IJobMessage" />.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the send operation.</param>
    /// <typeparam name="TMessage">The job message type. Must implement <see cref="IJobMessage" />.</typeparam>
    /// <returns>
    ///     A <see cref="JobId" /> that uniquely identifies the queued job and can be used to query its state via
    ///     <see cref="IJobTrackingService" />.
    /// </returns>
    Task<JobId> QueueJob<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IJobMessage;

    /// <summary>
    ///     Triggers an immediate execution of the recurring job identified by <paramref name="jobId" />, bypassing the
    ///     normal CRON schedule. Used to manually start or re-run a recurring job on demand.
    /// </summary>
    /// <param name="jobId">The identifier of the recurring job to trigger.</param>
    Task StartRecurringJob(Guid jobId);
}