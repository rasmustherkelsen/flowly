using SimpleTransit.MessageInfrastructure.Model;

namespace SimpleTransit.MessageInfrastructure.Senders;

internal interface IRecurringJobInvoker
{
    Task Submit(RecurringJob recurringJob);
}