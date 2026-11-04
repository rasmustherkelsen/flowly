using System.Diagnostics;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.MessageInfrastructure.Callers;

internal class CallSubmitter<TMessage, TReturn>(
    IMessageBusClientRegistry clientRegistry,
    string callQueueName,
    string returnQueueName,
    string providerName,
    TimeSpan timeout,
    IPendingCallRegistry<TReturn> pendingCallRegistry,
    ICallerInstrumentation callerInstrumentation) : ICallSubmitter<TMessage, TReturn>
    where TMessage : class, IReturns<TReturn>
    where TReturn : class
{
    public async Task<TReturn> Submit(TMessage message, CancellationToken cancellationToken)
    {
        var client = clientRegistry.GetClient(providerName);
        var correlationId = Guid.NewGuid().ToString();
        var messageId = Guid.NewGuid().ToString();
        var sw = Stopwatch.StartNew();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var responseTask = pendingCallRegistry.Register(correlationId, timeoutCts.Token);

        using var activity = callerInstrumentation.StartCalling(callQueueName, client.MessagingSystem, messageId, correlationId);
        activity.ApplyTagsFrom(message);

        try
        {
            var sender = await client.CreateMessageBusSender(callQueueName);

            await sender.SendMessage(
                message,
                new MessageProperties(messageId, correlationId, ReplyTo: returnQueueName),
                cancellationToken);

            var response = await responseTask;

            callerInstrumentation.RecordSucceeded(callQueueName, sw.Elapsed.TotalMilliseconds);

            return response;
        }
        catch
        {
            callerInstrumentation.RecordFailed(callQueueName);
            throw;
        }
    }
}
