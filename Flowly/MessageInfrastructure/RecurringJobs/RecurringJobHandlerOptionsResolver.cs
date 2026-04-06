using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Flowly.MessageInfrastructure.RecurringJobs;

public static class RecurringJobHandlerOptionsResolver
{
    public static ResolvedRecurringJobHandlerOptions Resolve<TRecurringJobHandler>() where TRecurringJobHandler : class, IRecurringJobHandler
    {
        var handlerType = typeof(TRecurringJobHandler);
        var options = new RecurringJobHandlerOptions();

        ApplyAttributes(handlerType, options);
        ApplyConfigure(handlerType, options);

        var resolvedDescription = string.IsNullOrWhiteSpace(options.JobDescription)
            ? DeriveDefaultDescription(handlerType)
            : options.JobDescription!;

        var resolvedCronExpression = string.IsNullOrWhiteSpace(options.CronExpression)
            ? throw new InvalidOperationException($"Recurring job handler '{handlerType.Name}' must define a cron expression via {nameof(RecurringJobAttribute)} or {nameof(RecurringJobHandlerBase.Configure)}.")
            : options.CronExpression!;

        return new ResolvedRecurringJobHandlerOptions(resolvedDescription, resolvedCronExpression);
    }

    private static void ApplyAttributes(Type handlerType, RecurringJobHandlerOptions options)
    {
        var recurringJobAttribute = handlerType.GetCustomAttribute<RecurringJobAttribute>();
        if (recurringJobAttribute != null)
        {
            options.JobDescription = recurringJobAttribute.JobDescription;
            options.CronExpression = recurringJobAttribute.CronExpression;
        }

        if (string.IsNullOrWhiteSpace(options.JobDescription))
        {
            var descriptionAttribute = handlerType.GetCustomAttribute<DescriptionAttribute>();
            if (descriptionAttribute != null)
            {
                options.JobDescription = descriptionAttribute.Description;
            }
        }
    }

    private static void ApplyConfigure(Type handlerType, RecurringJobHandlerOptions options)
    {
        var configureMethod = handlerType.GetMethod(nameof(RecurringJobHandlerBase.Configure), [typeof(RecurringJobHandlerOptions)]);

        if (configureMethod is null || configureMethod.DeclaringType == typeof(RecurringJobHandlerBase))
        {
            return;
        }

        if (CreateHandlerInstanceForConfigure(handlerType) is not { } handlerInstance)
        {
            return;
        }

        configureMethod.Invoke(handlerInstance, [options]);
    }

    private static object? CreateHandlerInstanceForConfigure(Type handlerType)
    {
        try
        {
            return Activator.CreateInstance(handlerType);
        }
        catch
        {
            try
            {
                return RuntimeHelpers.GetUninitializedObject(handlerType);
            }
            catch
            {
                return null;
            }
        }
    }

    private static string DeriveDefaultDescription(Type handlerType)
    {
        var name = handlerType.Name;

        if (name.EndsWith("RecurringJob", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^"RecurringJob".Length];
        }

        if (name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^"Handler".Length];
        }

        return name;
    }
}
