using System.Reflection;
using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Callers;
using Flowly.MessageInfrastructure.MessageHandlingStrategies;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flowly;

/// <summary>
///     Provides extension methods for registering call handlers and call submitters in the Flowly framework.
/// </summary>
public static class CallHandlerRegistrationExtensions
{
    /// <summary>
    ///     Registers a call handler for the specified message type. The handler receives call messages on the
    ///     call queue and routes the response back to the originating caller's reply queue.
    ///     Supports the same queue-level configuration as <see cref="MessageHandlerRegistrationExtensions.AddMessageHandler{TMessage,THandler}" />:
    ///     retry policy, queue name overrides, concurrency, and provider affinity.
    /// </summary>
    /// <param name="flowlyBuilder">The Flowly builder.</param>
    /// <typeparam name="TMessage">
    ///     The call message type. Must implement <see cref="IReturns{TReturn}" /> for some
    ///     <c>TReturn</c>.
    /// </typeparam>
    /// <typeparam name="THandler">The call handler implementation type.</typeparam>
    /// <returns>The same <see cref="IFlowlyBuilder" /> for further configuration.</returns>
    public static IFlowlyBuilder AddCallHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder)
        where TMessage : class
        where THandler : class
    {
        var returnType = GetReturnType(typeof(TMessage));

        var registerMethod = typeof(CallHandlerRegistrationExtensions)
            .GetMethod(nameof(RegisterCallHandler), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(TMessage), typeof(THandler), returnType);

        registerMethod.Invoke(null, [flowlyBuilder]);

        return flowlyBuilder;
    }

    /// <summary>
    ///     Registers a call submitter for <typeparamref name="TMessage" /> so that calls can be made using
    ///     <see cref="IMessageCaller.Call{TMessage,TReturn}" />. Stands up a background listener on the per-instance
    ///     reply queue and registers <see cref="IMessageCaller" /> in the DI container.
    ///     Requires <see cref="FlowlyOptions.InstanceName" /> to be set; throws
    ///     <see cref="InvalidOperationException" /> if it is not.
    /// </summary>
    /// <param name="flowlyBuilder">The Flowly builder.</param>
    /// <param name="configure">Optional delegate to configure per-submitter options such as the call timeout.</param>
    /// <typeparam name="TMessage">
    ///     The call message type. Must implement <see cref="IReturns{TReturn}" /> for some
    ///     <c>TReturn</c>.
    /// </typeparam>
    /// <returns>The same <see cref="IFlowlyBuilder" /> for further configuration.</returns>
    public static IFlowlyBuilder AddCallSubmitter<TMessage>(
        this IFlowlyBuilder flowlyBuilder,
        Action<CallSubmitterOptions>? configure = null)
        where TMessage : class
    {
        using var serviceProvider = flowlyBuilder.Services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<FlowlyOptions>();

        if (string.IsNullOrWhiteSpace(options.InstanceName))
            throw new InvalidOperationException(
                $"FlowlyOptions.InstanceName must be set before registering a call submitter for '{typeof(TMessage).Name}'. " +
                "Set it via the options delegate: AddFlowly<Config>(opts => opts.InstanceName = \"my-service\").");

        var submitterOptions = new CallSubmitterOptions();
        configure?.Invoke(submitterOptions);
        var timeout = submitterOptions.Timeout ?? options.MessageCallTimeout;

        var returnType = GetReturnType(typeof(TMessage));
        var callQueueName = flowlyBuilder.TopologyNameResolver.ResolveQueueName<TMessage>();
        var returnQueueName = $"{callQueueName}.reply.{options.InstanceName}";
        var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, typeof(TMessage));

        var queueRegistrar = serviceProvider.GetRequiredService<IQueueRegistrar>();
        queueRegistrar.Register(
            flowlyBuilder.Services,
            new DeferredQueueRegistration(returnQueueName, IsReplyQueue: true),
            providerName);

        var registerReturnQueueMethod = typeof(CallHandlerRegistrationExtensions)
            .GetMethod(nameof(RegisterReturnQueueInfrastructure), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(returnType);

        registerReturnQueueMethod.Invoke(null, [flowlyBuilder.Services, returnQueueName, providerName]);

        var registerSubmitterMethod = typeof(CallHandlerRegistrationExtensions)
            .GetMethod(nameof(RegisterCallSubmitter), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(TMessage), returnType);

        registerSubmitterMethod.Invoke(null, [flowlyBuilder.Services, callQueueName, returnQueueName, providerName, timeout]);

        flowlyBuilder.Services.TryAddSingleton<IMessageCaller, MessageCaller>();

        return flowlyBuilder;
    }

    private static void RegisterCallHandler<TMessage, THandler, TReturn>(IFlowlyBuilder flowlyBuilder)
        where TMessage : class, IReturns<TReturn>
        where THandler : CallHandler<TMessage, TReturn>
        where TReturn : class
    {
        flowlyBuilder.Services.AddScoped<CallHandler<TMessage, TReturn>, THandler>();
        flowlyBuilder.AddMessageProcessor<TMessage, THandler,
            MessageProcessingBackgroundService<TMessage>,
            CallMessageHandlingStrategy<TMessage, TReturn>>();
    }

    private static void RegisterReturnQueueInfrastructure<TReturn>(
        IServiceCollection services,
        string returnQueueName,
        string providerName)
        where TReturn : class
    {
        services.TryAddSingleton<IPendingCallRegistry<TReturn>, PendingCallRegistry<TReturn>>();
        services.AddSingleton(new CallReturnQueueSettings<TReturn>(returnQueueName, providerName));
        services.AddHostedService<CallReturnQueueBackgroundService<TReturn>>();
    }

    private static void RegisterCallSubmitter<TMessage, TReturn>(
        IServiceCollection services,
        string callQueueName,
        string returnQueueName,
        string providerName,
        TimeSpan timeout)
        where TMessage : class, IReturns<TReturn>
        where TReturn : class
    {
        services.AddSingleton(new CallSubmitter<TMessage, TReturn>.Settings(callQueueName, returnQueueName, providerName, timeout));
        services.AddSingleton<ICallSubmitter<TMessage, TReturn>, CallSubmitter<TMessage, TReturn>>();
    }

    private static Type GetReturnType(Type messageType)
    {
        var iReturns = messageType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReturns<>));

        if (iReturns == null)
            throw new InvalidOperationException(
                $"Message type '{messageType.Name}' does not implement IReturns<TReturn>. " +
                "Call messages must implement IReturns<TReturn> to declare their response type.");

        return iReturns.GetGenericArguments()[0];
    }
}
