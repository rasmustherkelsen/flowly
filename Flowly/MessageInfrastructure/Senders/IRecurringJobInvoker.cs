using Flowly.MessageInfrastructure.Model;

namespace Flowly.MessageInfrastructure.Senders;

internal interface IRecurringJobInvoker
{
    Task Submit(RecurringJob recurringJob);
}