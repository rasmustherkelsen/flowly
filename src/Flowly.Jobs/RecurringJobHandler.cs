namespace Flowly.Jobs;

/// <summary>
///     Base class for recurring job handlers in the Flowly framework. This abstract class provides a template for
///     implementing recurring job handlers by defining a method for handling the job and an optional configuration method.
///     Derived classes must implement the Handle method to define the logic for the recurring job, while the Configure
///     method can be overridden to set up any necessary options or parameters for the job. This base class ensures a
///     consistent structure for all recurring job handlers within the Flowly framework, allowing for easy integration and
///     management of recurring tasks.
/// </summary>
public abstract class RecurringJobHandler
{
    /// <summary>
    ///     Handles the recurring job logic. Derived classes must implement this method to define the specific actions to be
    ///     performed when the recurring job is executed. The method takes a CancellationToken as a parameter, allowing for
    ///     graceful cancellation of the job if needed. This method will be called by the recurring job scheduler at the
    ///     specified intervals, and it should contain the core logic for the task that needs to be performed on a recurring
    ///     basis.
    /// </summary>
    /// <param name="cancellationToken">Used to determine if the job should be aborted.</param>
    /// <returns>A Task representing the asynchronous operation of handling the recurring job.</returns>
    public abstract Task Handle(CancellationToken cancellationToken);

    /// <summary>
    ///     Configures the recurring job options. This method can be overridden by derived classes to set up any necessary
    ///     options or parameters for the recurring job.
    /// </summary>
    /// <param name="options">A valid <see cref="RecurringJobHandlerOptions" /> instance where options can be applied.</param>
    public virtual void Configure(RecurringJobHandlerOptions options)
    {
    }
}