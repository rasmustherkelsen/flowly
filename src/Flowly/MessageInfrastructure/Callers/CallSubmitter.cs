using System.Diagnostics;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.MessageInfrastructure.Callers;

internal class CallSubmitter<TMessage, TReturn>(
    IMessageBusClientRegistry clientRegistry,
    CallSubmitter<TMessage, TReturn>.Settings settings,
    IPendingCallRegistry<TReturn> pendingCallRegistry,
    ICallerInstrumentation callerInstrumentation) : ICallSubmitter<TMessage, TReturn>
    where TMessage : class, IReturns<TReturn>
    where TReturn : class
{
    public class Settings(string callQueueName, string returnQueueName, string providerName, TimeSpan timeout)
    {
        public string CallQueueName { get; } = callQueueName;
        public string ReturnQueueName { get; } = returnQueueName;
        public string ProviderName { get; } = providerName;
        public TimeSpan Timeout { get; } = timeout;
    }

    public async Task<TReturn> Submit(TMessage message, CancellationToken cancellationToken)
    {
        var client = clientRegistry.GetClient(settings.ProviderName);
        var correlationId = Guid.NewGuid().ToString();
        var messageId = Guid.NewGuid().ToString();
        var sw = Stopwatch.StartNew();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(settings.Timeout);

        var responseTask = pendingCallRegistry.Register(correlationId, timeoutCts.Token);

        using var activity = callerInstrumentation.StartCalling(settings.CallQueueName, client.MessagingSystem, messageId, correlationId);
        activity.ApplyTagsFrom(message);

        try
        {
            var sender = await client.CreateMessageBusSender(settings.CallQueueName);

            await sender.SendMessage(
                message,
                new MessageProperties(messageId, correlationId, ReplyTo: settings.ReturnQueueName),
                cancellationToken);

            var response = await responseTask;

            callerInstrumentation.RecordSucceeded(settings.CallQueueName, sw.Elapsed.TotalMilliseconds);

            return response;
        }
        catch
        {
            callerInstrumentation.RecordFailed(settings.CallQueueName);
            throw;
        }
    }
}
