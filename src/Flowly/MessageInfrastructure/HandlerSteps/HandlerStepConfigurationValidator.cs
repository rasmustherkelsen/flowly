using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.HandlerSteps;

/// <summary>
///     Logs a warning at host start-up for handler step registrations that are almost certainly mistakes: a step whose
///     message filter matches the message type of no registered handler, and a step type that is registered more than once.
///     Runs at start-up rather than at registration time so the order of step and handler registrations does not matter.
/// </summary>
internal sealed class HandlerStepConfigurationValidator(
    IEnumerable<HandlerStepRegistration> registrations,
    RegisteredHandlerMessageTypes registeredHandlerMessageTypes,
    ILogger<HandlerStepConfigurationValidator> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var stepRegistrations = registrations.ToArray();

        if (stepRegistrations.Length == 0)
        {
            return Task.CompletedTask;
        }

        WarnAboutDuplicates(stepRegistrations);
        WarnAboutStepsWithoutMatchingHandler(stepRegistrations);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void WarnAboutDuplicates(HandlerStepRegistration[] stepRegistrations)
    {
        foreach (var duplicate in stepRegistrations.GroupBy(registration => registration.StepType).Where(group => group.Count() > 1))
        {
            logger.LogWarning(
                "Handler step '{StepType}' is registered {Count} times and will run {Count} times for every matching message. Register it once.",
                duplicate.Key,
                duplicate.Count(),
                duplicate.Count());
        }
    }

    private void WarnAboutStepsWithoutMatchingHandler(HandlerStepRegistration[] stepRegistrations)
    {
        var handlerMessageTypes = registeredHandlerMessageTypes.GetAll();

        foreach (var registration in stepRegistrations.DistinctBy(registration => registration.StepType))
        {
            if (!handlerMessageTypes.Any(handlerMessageType => registration.FilterType.IsAssignableFrom(handlerMessageType)))
            {
                logger.LogWarning(
                    "Handler step '{StepType}' has the message filter '{FilterType}', which matches the message type of no registered message, event, call or job handler, so the step will never run. " +
                    "Check the filter type and that the step is registered in the service that hosts the handlers.",
                    registration.StepType,
                    registration.FilterType);
            }
        }
    }
}
