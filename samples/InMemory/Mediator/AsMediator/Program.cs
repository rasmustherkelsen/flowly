using AsMediator.CommandHandlers;
using AsMediator.Commands;
using AsMediator.DataContracts;
using AsMediator.Repositories;
using Flowly;
using Flowly.InMemory;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(null, flowlyBuilder =>
{
    flowlyBuilder
        .UseInMemory("Mediator", options => options.EnableReferencePassing = true)
        .AddMessageSubmitter<CreateContactCommand>()
        .AddMessageHandler<CreateContactCommand, CreateContactCommandHandler>();
});

builder.Services.AddSingleton<ContactsRepository>();

var app = builder.Build();

app.MapPost("contacts", async (CreateContact contact, IMessageSender messageSender) =>
{
    var command = new CreateContactCommand(Guid.NewGuid(), contact.Name);
    await messageSender.Send(command);
    return Results.CreatedAtRoute("GetContact", new { command.Id }, contact);
});

app.MapGet("contacts/{id}", (Guid id, [FromServices] ContactsRepository contactsRepository) =>
    {
        var contact = contactsRepository.GetContact(id);
        return contact != null ? Results.Ok(contact) : Results.NotFound();
    })
    .WithName("GetContact");

app.Run();