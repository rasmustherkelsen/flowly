using AsMediator.Commands;
using AsMediator.Repositories;
using Flowly;

namespace AsMediator.CommandHandlers;

internal class CreateContactCommandHandler(ContactsRepository contactsRepository) : MessageHandler<CreateContactCommand>
{
    public override Task Handle(IMessageContext<CreateContactCommand> messageContext)
    {
        contactsRepository.AddContact(new(messageContext.Message.Id, messageContext.Message.Name));
        return Task.CompletedTask;
    }
}