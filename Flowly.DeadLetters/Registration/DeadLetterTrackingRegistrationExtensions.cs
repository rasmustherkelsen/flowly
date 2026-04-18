using Flowly.DeadLetters.BackgroundServices;
using Flowly.DeadLetters.Repositories;
using Flowly.DeadLetters.Services;
using Flowly.DeadLetters.Telemetry;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.DeadLetters.Registration;

public static class DeadLetterTrackingRegistrationExtensions
{
    private sealed class DeadLetterTrackingSentinel;

    private static readonly Type SentinelType = typeof(DeadLetterTrackingSentinel);

    /// <summary>
    /// Enables the dead letter persistence layer. Call this before using .WithDeadLetterTracking() on handlers.
    /// Typically called via a provider-specific extension (AddSqlServerDeadLetterTracking / AddPostgresDeadLetterTracking).
    /// </summary>
    public static IFlowlyBuilder AddDeadLetterTracking(this IFlowlyBuilder flowlyBuilder, Action<DbContextOptionsBuilder> dbContextOptions)
    {
        if (flowlyBuilder.Services.Any(s => s.ServiceType == SentinelType))
            return flowlyBuilder;

        flowlyBuilder.Services.AddSingleton<DeadLetterTrackingSentinel>();
        flowlyBuilder.Services.AddOptions<DeadLetterTrackingOptions>();
        flowlyBuilder.Services.AddDbContextFactory<DatabaseModel.DeadLetterDataContext>(dbContextOptions);
        flowlyBuilder.Services.AddScoped<IDeadLetterRepository, DeadLetterRepository>();
        flowlyBuilder.Services.AddScoped<IDeadLetterService, DeadLetterService>();
        flowlyBuilder.Services.AddSingleton(new DeadLetterIngestionHealthSettings(CheckInterval: TimeSpan.FromMinutes(5), StallThreshold: TimeSpan.FromMinutes(5)));
        flowlyBuilder.Services.AddHostedService<DeadLetterIngestionHealthBackgroundService>();
        flowlyBuilder.Services.AddHostedService<DeadLetterCleanupBackgroundService>();

        flowlyBuilder.Services.AddSingleton<DeadLetterGaugeMetrics>();
        flowlyBuilder.Services.AddHostedService<DeadLetterMetricsBackgroundService>();

        return flowlyBuilder;
    }

    /// <summary>
    /// Opts this handler's queue into dead letter tracking. Messages that are dead-lettered will be
    /// read from the broker DLQ and persisted to the dead letter store.
    /// Requires AddSqlServerDeadLetterTracking or AddPostgresDeadLetterTracking to have been called.
    /// </summary>
    public static IFlowlyBuilder WithDeadLetterTracking<TMessage>(this IMessageHandlerBuilder<TMessage> builder)
    {
        if (builder.Services.All(s => s.ServiceType != SentinelType))
            throw new InvalidOperationException("Dead letter tracking is not configured. Call AddSqlServerDeadLetterTracking() or AddPostgresDeadLetterTracking() before using WithDeadLetterTracking().");

        var queueName = builder.QueueName;
        var providerName = builder.ProviderName;

        if (builder.Services.Any(s => s.ServiceType == typeof(DeadLetterIngestionSettings) && s.ImplementationInstance is DeadLetterIngestionSettings ds && ds.QueueName == queueName))
            return builder;

        builder.Services.AddSingleton(new DeadLetterIngestionSettings(queueName, providerName));
        builder.Services.AddHostedService<DeadLetterIngestionBackgroundService>();

        return builder;
    }

    /// <summary>
    /// Opts this event subscription into dead letter tracking. Events that are dead-lettered by this subscriber
    /// will be read from the broker subscription DLQ and persisted to the dead letter store.
    /// Requeuing a dead-lettered event republishes it to the topic, so all subscribers will receive it again —
    /// ensure handlers are idempotent before using requeue.
    /// Requires AddSqlServerDeadLetterTracking or AddPostgresDeadLetterTracking to have been called.
    /// </summary>
    public static IFlowlyBuilder WithDeadLetterTracking<TEvent>(this IEventHandlerBuilder<TEvent> builder)
        where TEvent : class
    {
        if (builder.Services.All(s => s.ServiceType != SentinelType))
            throw new InvalidOperationException("Dead letter tracking is not configured. Call AddSqlServerDeadLetterTracking() or AddPostgresDeadLetterTracking() before using WithDeadLetterTracking().");

        var topicOrExchangeName = builder.TopicOrExchangeName;
        var subscriptionName = builder.SubscriptionName;
        var providerName = builder.ProviderName;

        if (builder.Services.Any(s =>
                s.ServiceType == typeof(EventSubscriptionDeadLetterIngestionSettings) &&
                s.ImplementationInstance is EventSubscriptionDeadLetterIngestionSettings existing &&
                existing.TopicOrExchangeName == topicOrExchangeName &&
                existing.SubscriptionName == subscriptionName))
            return builder;

        builder.Services.AddSingleton(new EventSubscriptionDeadLetterIngestionSettings(topicOrExchangeName, subscriptionName, providerName));
        builder.Services.AddHostedService<EventSubscriptionDeadLetterIngestionBackgroundService>();

        return builder;
    }
}