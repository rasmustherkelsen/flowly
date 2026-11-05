using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.HandlerSteps;

/// <summary>
///     Discovers the message types that have a handler registered in a service collection, by reading the
///     <see cref="IHandlerSettings{TMesssage}" /> registrations (message, batch, call and job handlers) and the
///     <see cref="EventHandlerSettings{TEvent, THandler}" /> registrations (event handlers). The collection is read when
///     <see cref="GetAll" /> is called, so the result does not depend on registration order.
/// </summary>
internal sealed class RegisteredHandlerMessageTypes(IServiceCollection services)
{
    public IReadOnlyList<Type> GetAll()
        => services
            .Select(descriptor => descriptor.ServiceType)
            .Where(IsHandlerSettingsType)
            .Select(serviceType => serviceType.GetGenericArguments()[0])
            .Distinct()
            .ToArray();

    private static bool IsHandlerSettingsType(Type serviceType)
    {
        if (!serviceType.IsGenericType)
        {
            return false;
        }

        var definition = serviceType.GetGenericTypeDefinition();

        return definition == typeof(IHandlerSettings<>) || definition == typeof(EventHandlerSettings<,>);
    }
}
