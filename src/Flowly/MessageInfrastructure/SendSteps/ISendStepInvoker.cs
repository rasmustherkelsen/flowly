namespace Flowly.MessageInfrastructure.SendSteps;

internal interface ISendStepInvoker
{
    Task Invoke(object message, CancellationToken cancellationToken, SendStepDelegate next);
}
