using System.Reflection;
using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Flowly.Jobs;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Dashboard;

/// <summary>
///     Builds the submitter lookup table from <see cref="FlowlySubmitterManifest" /> and
///     dispatches runtime submit requests to <see cref="IMessageSender" />, <see cref="IEventSender" />,
///     or <see cref="IMessageCaller" />.
/// </summary>
public sealed class SubmitterDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private readonly IReadOnlyList<SubmitterInfo> _submitters;

    /// <summary>
    ///     Initialises the dispatcher and generates a JSON schema for each registered submitter type.
    /// </summary>
    /// <param name="manifest">The submitter manifest populated during application configuration.</param>
    public SubmitterDispatcher(FlowlySubmitterManifest manifest)
    {
        var exporterOptions = new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true };

        _submitters = manifest.Submitters
            .Select(r =>
            {
                var schema = JsonOptions.GetJsonSchemaAsNode(r.MessageType, exporterOptions);
                return new SubmitterInfo(r.MessageType, r.MessageType.Name, r.QueueOrTopicName, r.Kind, schema.ToString());
            })
            .ToList();
    }

    /// <summary>All submittable message types with their resolved metadata and JSON schema.</summary>
    public IReadOnlyList<SubmitterInfo> Submitters => _submitters;

    /// <summary>
    ///     Deserialises the payload and dispatches the message to the appropriate sender.
    /// </summary>
    /// <param name="typeName">The simple type name of the message (e.g. <c>OrderPlaced</c>).</param>
    /// <param name="payload">The raw JSON payload to deserialise.</param>
    /// <param name="serviceProvider">The DI scope to resolve senders from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    ///     A JSON response string for <see cref="SubmitterKind.Call" /> submitters (the return value) or
    ///     <see cref="SubmitterKind.Job" /> submitters (the <see cref="Flowly.Jobs.Model.JobId" />);
    ///     <see langword="null" /> for fire-and-forget kinds.
    /// </returns>
    public async Task<string?> Dispatch(
        string typeName,
        string payload,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var info = _submitters.FirstOrDefault(s => s.TypeName == typeName)
            ?? throw new KeyNotFoundException($"No submitter registered for type '{typeName}'.");

        var message = JsonSerializer.Deserialize(payload, info.MessageType, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialise payload for '{typeName}'.");

        return info.Kind switch
        {
            SubmitterKind.Message => await DispatchMessage(info.MessageType, message, serviceProvider, cancellationToken),
            SubmitterKind.Event   => await DispatchEvent(info.MessageType, message, serviceProvider, cancellationToken),
            SubmitterKind.Call    => await DispatchCall(info.MessageType, message, serviceProvider, cancellationToken),
            SubmitterKind.Job     => await DispatchJob(info.MessageType, message, serviceProvider, cancellationToken),
            _                     => throw new InvalidOperationException($"Unknown submitter kind '{info.Kind}'.")
        };
    }

    private static async Task<string?> DispatchMessage(
        Type messageType,
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var sender = serviceProvider.GetRequiredService<IMessageSender>();
        var method = typeof(IMessageSender)
            .GetMethod(nameof(IMessageSender.Send))!
            .MakeGenericMethod(messageType);

        await (Task)method.Invoke(sender, [message, cancellationToken])!;
        return null;
    }

    private static async Task<string?> DispatchEvent(
        Type eventType,
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var sender = serviceProvider.GetRequiredService<IEventSender>();
        var method = typeof(IEventSender)
            .GetMethod(nameof(IEventSender.RaiseEvent))!
            .MakeGenericMethod(eventType);

        await (Task)method.Invoke(sender, [message, cancellationToken])!;
        return null;
    }

    private static async Task<string?> DispatchCall(
        Type messageType,
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var returnType = GetReturnType(messageType);
        var caller = serviceProvider.GetRequiredService<IMessageCaller>();
        var method = typeof(IMessageCaller)
            .GetMethod(nameof(IMessageCaller.Call))!
            .MakeGenericMethod(messageType, returnType);

        var task = (Task)method.Invoke(caller, [message, cancellationToken])!;
        await task;

        var resultProperty = task.GetType().GetProperty(nameof(Task<object>.Result))!;
        var result = resultProperty.GetValue(task);

        return result is null ? null : JsonSerializer.Serialize(result, JsonOptions);
    }

    private static async Task<string?> DispatchJob(
        Type messageType,
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var sender = serviceProvider.GetRequiredService<IJobMessageSender>();
        var method = typeof(IJobMessageSender)
            .GetMethod(nameof(IJobMessageSender.QueueJob))!
            .MakeGenericMethod(messageType);

        var task = (Task)method.Invoke(sender, [message, cancellationToken])!;
        await task;

        var resultProperty = task.GetType().GetProperty(nameof(Task<object>.Result))!;
        var jobId = resultProperty.GetValue(task);

        return jobId is null ? null : JsonSerializer.Serialize(jobId, JsonOptions);
    }

    private static Type GetReturnType(Type messageType)
    {
        var iReturns = messageType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReturns<>));

        return iReturns?.GetGenericArguments()[0]
            ?? throw new InvalidOperationException(
                $"Call message type '{messageType.Name}' does not implement IReturns<TReturn>.");
    }
}

/// <summary>
///     Describes a single submittable message type exposed by the dashboard.
/// </summary>
/// <param name="MessageType">The CLR type of the message.</param>
/// <param name="TypeName">The simple name used to identify this submitter in API calls.</param>
/// <param name="QueueOrTopicName">The resolved queue or topic name.</param>
/// <param name="Kind">The kind of submitter.</param>
/// <param name="JsonSchema">The JSON Schema string describing the message payload structure.</param>
public record SubmitterInfo(
    Type MessageType,
    string TypeName,
    string QueueOrTopicName,
    SubmitterKind Kind,
    string JsonSchema);
