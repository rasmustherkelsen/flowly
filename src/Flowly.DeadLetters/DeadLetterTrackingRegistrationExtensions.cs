using Flowly.DeadLetters;
using Flowly.DeadLetters.BackgroundServices;
using Flowly.DeadLetters.DatabaseModel;
using Flowly.DeadLetters.Repositories;
using Flowly.DeadLetters.Services;
using Flowly.DeadLetters.Telemetry;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flowly;

/// <summary>
///     Extension methods for <see cref="IFlowlyBuilder"/> that configure dead letter tracking.
///     Call <c>AddSqlServerDeadLetterTracking</c> or <c>AddPostgresDeadLetterTracking</c> once on the builder to
///     enable the persistence layer, then chain <c>.WithDeadLetterTracking()</c> on individual handler or event
///     handler registrations to opt specific queues or subscriptions into dead letter ingestion.
///     Use <c>AddDeadLetterSource&lt;TMessage&gt;()</c> in a standalone tracker project to monitor a queue's
///     dead-letter sub-queue without registering a consumer on the main queue.
/// </summary>
public static class DeadLetterTrackingRegistrationExtensions
{
    private static readonly Type SentinelType = typeof(DeadLetterTrackingSentinel);

    /// <summary>
    ///     Enables the dead letter persistence layer by registering the EF Core data context, repository, service,
    ///     and supporting background services. This method is idempotent — calling it more than once has no effect.
    ///     Prefer the provider-specific overloads (<c>AddSqlServerDeadLetterTracking</c> /
    ///     <c>AddPostgresDeadLetterTracking</c>) over calling this directly.
    /// </summary>
    /// <param name="flowlyBuilder">The <see cref="IFlowlyBuilder"/> to configure.</param>
    /// <param name="dbContextOptions">A delegate that configures the <see cref="DbContextOptionsBuilder"/> for the dead letter data context.</param>
    /// <returns>The same <see cref="IFlowlyBuilder"/> for chaining.</returns>
    public static IFlowlyBuilder AddDeadLetterTracking(this IFlowlyBuilder flowlyBuilder, Action<DbContextOptionsBuilder> dbContextOptions)
    {
        if (flowlyBuilder.Services.Any(s => s.ServiceType == SentinelType))
            return flowlyBuilder;

        flowlyBuilder.Services.AddSingleton<DeadLetterTrackingSentinel>();
        flowlyBuilder.Services.AddOptions<DeadLetterTrackingOptions>();
        flowlyBuilder.Services.AddDbContextFactory<DeadLetterDataContext>(dbContextOptions);
        flowlyBuilder.Services.AddScoped<IDeadLetterRepository, DeadLetterRepository>();
        flowlyBuilder.Services.AddScoped<IDeadLetterService, DeadLetterService>();
        flowlyBuilder.Services.AddSingleton(new DeadLetterIngestionHealthSettings(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)));
        flowlyBuilder.Services.AddHostedService<DeadLetterIngestionHealthBackgroundService>();
        flowlyBuilder.Services.AddHostedService<DeadLetterCleanupBackgroundService>();

        flowlyBuilder.Services.AddSingleton<DeadLetterGaugeMetrics>();
        flowlyBuilder.Services.AddHostedService<DeadLetterMetricsBackgroundService>();

        flowlyBuilder.Services.TryAddSingleton<IDeadLetterOperationInstrumentation>(sp =>
        {
            var options = sp.GetRequiredService<FlowlyOptions>();
            return options.EnableTelemetry
                ? new DeadLetterOperationInstrumentation()
                : new NullDeadLetterOperationInstrumentation();
        });

        return flowlyBuilder;
    }

    /// <summary>
    ///     Opts this handler's queue into dead letter tracking. Messages that are dead-lettered will be read from the
    ///     broker dead letter queue and persisted to the tracking store.
    ///     Requires <c>AddSqlServerDeadLetterTracking</c> or <c>AddPostgresDeadLetterTracking</c> to have been called first.
    ///     Throws <see cref="InvalidOperationException"/> if the dead letter tracking infrastructure has not been registered.
    /// </summary>
    /// <typeparam name="TMessage">The message type handled by the handler being registered.</typeparam>
    /// <param name="builder">The <see cref="IMessageHandlerBuilder{TMessage}"/> for the handler to opt in.</param>
    /// <returns>The parent <see cref="IFlowlyBuilder"/> for chaining.</returns>
    public static IFlowlyBuilder WithDeadLetterTracking<TMessage>(this IMessageHandlerBuilder<TMessage> builder)
    {
        if (builder.Services.All(s => s.ServiceType != SentinelType))
            throw new InvalidOperationException("Dead letter tracking is not configured. Call AddSqlServerDeadLetterTracking() or AddPostgresDeadLetterTracking() before using WithDeadLetterTracking().");

        var handlerSettings = builder.Services.BuildServiceProvider().GetRequiredService<IHandlerSettings<TMessage>>();

        var queueName = handlerSettings.QueueName;
        var providerName = handlerSettings.ProviderName;

        if (builder.Services.Any(s => s.ServiceType == typeof(DeadLetterIngestionSettings) && s.ImplementationInstance is DeadLetterIngestionSettings ds && ds.QueueName == queueName))
            return builder;

        builder.Services.AddSingleton(new DeadLetterIngestionSettings(queueName, providerName));
        builder.Services.AddHostedService<DeadLetterIngestionBackgroundService>();

        return builder;
    }

    /// <summary>
    ///     Opts this event subscription into dead letter tracking. Events that are dead-lettered by this subscriber will
    ///     be read from the broker subscription dead letter queue and persisted to the tracking store.
    ///     When a dead-lettered event is requeued it is republished to the topic with a <c>flowly-target-subscription</c>
    ///     property so that only the originating subscription receives the requeued message — ensure your handler is
    ///     idempotent before using requeue.
    ///     Requires <c>AddSqlServerDeadLetterTracking</c> or <c>AddPostgresDeadLetterTracking</c> to have been called first.
    ///     Throws <see cref="InvalidOperationException"/> if the dead letter tracking infrastructure has not been registered.
    /// </summary>
    /// <typeparam name="TEvent">The event type handled by the event handler being registered.</typeparam>
    /// <param name="builder">The <see cref="IEventHandlerBuilder{TEvent}"/> for the event handler to opt in.</param>
    /// <returns>The parent <see cref="IFlowlyBuilder"/> for chaining.</returns>
    public static IFlowlyBuilder WithDeadLetterTracking<TEvent>(this IEventHandlerBuilder<TEvent> builder)
        where TEvent : class
    {
        if (builder.Services.All(s => s.ServiceType != SentinelType))
            throw new InvalidOperationException("Dead letter tracking is not configured. Call AddSqlServerDeadLetterTracking() or AddPostgresDeadLetterTracking() before using WithDeadLetterTracking().");

        var topicName = builder.TopicName;
        var subscriptionName = builder.SubscriptionName;
        var providerName = builder.ProviderName;

        if (builder.Services.Any(s =>
                s.ServiceType == typeof(EventSubscriptionDeadLetterIngestionSettings) &&
                s.ImplementationInstance is EventSubscriptionDeadLetterIngestionSettings existing &&
                existing.TopicName == topicName &&
                existing.SubscriptionName == subscriptionName))
            return builder;

        builder.Services.AddSingleton(new EventSubscriptionDeadLetterIngestionSettings(topicName, subscriptionName, providerName));
        builder.Services.AddHostedService<EventSubscriptionDeadLetterIngestionBackgroundService>();

        return builder;
    }

    /// <summary>
    ///     Registers dead letter ingestion for the queue derived from <typeparamref name="TMessage"/> without registering
    ///     a consumer on the main queue. Use this in a standalone <c>DeadletterTracker</c> project that is responsible
    ///     solely for reading dead-lettered messages from the broker and persisting them to the tracking store, while the
    ///     Receiver continues to process the main queue independently.
    ///     Requires <c>AddSqlServerDeadLetterTracking</c> or <c>AddPostgresDeadLetterTracking</c> to have been called first.
    ///     Throws <see cref="InvalidOperationException"/> if the dead letter tracking infrastructure has not been registered.
    /// </summary>
    /// <typeparam name="TMessage">
    ///     The message contract type whose queue name will be resolved and monitored for dead letters.
    /// </typeparam>
    /// <param name="flowlyBuilder">The <see cref="IFlowlyBuilder"/> to configure.</param>
    /// <returns>The same <see cref="IFlowlyBuilder"/> for chaining.</returns>
    public static IFlowlyBuilder AddDeadLetterSource<TMessage>(this IFlowlyBuilder flowlyBuilder)
        where TMessage : class
    {
        if (flowlyBuilder.Services.All(s => s.ServiceType != SentinelType))
            throw new InvalidOperationException("Dead letter tracking is not configured. Call AddSqlServerDeadLetterTracking() or AddPostgresDeadLetterTracking() before using AddDeadLetterSource().");

        var queueName = flowlyBuilder.TopologyNameResolver.ResolveQueueName<TMessage>();
        var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, typeof(TMessage));

        if (flowlyBuilder.Services.Any(s => s.ServiceType == typeof(DeadLetterIngestionSettings) && s.ImplementationInstance is DeadLetterIngestionSettings ds && ds.QueueName == queueName))
            return flowlyBuilder;

        flowlyBuilder.Services.AddSingleton(new DeadLetterIngestionSettings(queueName, providerName));
        flowlyBuilder.Services.AddHostedService<DeadLetterIngestionBackgroundService>();

        return flowlyBuilder;
    }

    /// <summary>
    ///     Registers only the EF Core data context, repository, and <see cref="IDeadLetterService"/> — no background
    ///     services for ingestion, cleanup, or metrics. Used by read-only consumers such as a standalone Dashboard
    ///     project that queries dead letter state but does not ingest or process it.
    /// </summary>
    /// <param name="flowlyBuilder">The <see cref="IFlowlyBuilder"/> to configure.</param>
    /// <param name="dbContextOptions">A delegate that configures the <see cref="DbContextOptionsBuilder"/> for the dead letter data context.</param>
    /// <returns>The same <see cref="IFlowlyBuilder"/> for chaining.</returns>
    internal static IFlowlyBuilder AddDeadLetterReadAccess(
        this IFlowlyBuilder flowlyBuilder,
        Action<DbContextOptionsBuilder> dbContextOptions)
    {
        flowlyBuilder.Services.AddDbContextFactory<DeadLetterDataContext>(dbContextOptions);
        flowlyBuilder.Services.AddScoped<IDeadLetterRepository, DeadLetterRepository>();
        flowlyBuilder.Services.AddScoped<IDeadLetterService, DeadLetterService>();

        flowlyBuilder.Services.TryAddSingleton<IDeadLetterOperationInstrumentation>(sp =>
        {
            var options = sp.GetRequiredService<FlowlyOptions>();
            return options.EnableTelemetry
                ? new DeadLetterOperationInstrumentation()
                : new NullDeadLetterOperationInstrumentation();
        });

        return flowlyBuilder;
    }

    private sealed class DeadLetterTrackingSentinel;
}