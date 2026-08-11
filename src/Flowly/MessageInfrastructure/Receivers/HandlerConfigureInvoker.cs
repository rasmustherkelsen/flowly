using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Flowly.MessageInfrastructure.Receivers;

internal static class HandlerConfigureInvoker
{
    public static object? CreateInstanceForConfigure(Type handlerType)
    {
        try
        {
            return Activator.CreateInstance(handlerType);
        }
        catch
        {
            try
            {
                var instance = RuntimeHelpers.GetUninitializedObject(handlerType);
                Trace.TraceWarning(
                    "Flowly: {0} has a Configure override but no parameterless constructor. " +
                    "Configure was invoked on an uninitialized instance — constructor-injected state will be null/default. " +
                    "Configure must not read fields set by the constructor.",
                    handlerType.FullName);

                return instance;
            }
            catch
            {
                Trace.TraceWarning(
                    "Flowly: {0} has a Configure override but could not be instantiated. " +
                    "Configure will be skipped and queue options will fall back to attribute defaults.",
                    handlerType.FullName);

                return null;
            }
        }
    }
}
