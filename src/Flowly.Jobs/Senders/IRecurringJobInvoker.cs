using Flowly.Jobs.Model;

namespace Flowly.Jobs.Senders;

internal interface IRecurringJobInvoker
{
    Task Submit(RecurringJob recurringJob);
}