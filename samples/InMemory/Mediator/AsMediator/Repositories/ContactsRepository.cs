using AsMediator.DataContracts;

namespace AsMediator.Repositories;

internal class ContactsRepository
{
    private readonly List<Contact> _contacts = new();

    public void AddContact(Contact contact)
        => _contacts.Add(contact);

    public Contact? GetContact(Guid id)
        => _contacts.SingleOrDefault(c => c.Id == id);
}