# InMemory — Mediator Pattern

Uses Flowly's InMemory transport as an in-process command dispatcher behind a minimal API, in the style of a mediator library — no external broker. `POST /contacts` dispatches a `CreateContactCommand` through `IMessageSender`, which an in-process `MessageHandler<T>` picks up and persists to a singleton repository.

## Projects

| Project | Purpose |
|---|---|
| `AsMediator` | Minimal API that dispatches `CreateContactCommand` via `IMessageSender` to an in-process `CreateContactCommandHandler` |

## What it demonstrates

- `UseInMemory(name, options => options.EnableReferencePassing = true)` — a named in-memory instance with reference passing enabled, skipping serialization since the sender and handler share a process
- Lambda-based configuration — `AddFlowly(null, flowlyBuilder => ...)` instead of a class-based `Configuration`
- `IMessageSender.Send()` called directly from a minimal API endpoint to dispatch a command
- `MessageHandler<T>` as an in-process command handler with a constructor-injected dependency (`ContactsRepository`)

## Prerequisites

- .NET 10 SDK (no Docker, no external broker or database)

## How to run

```bash
dotnet run --project AsMediator
```

Use the requests in `AsMediator.http` (or `curl`) to exercise the API:

```bash
curl -X POST http://localhost:<port>/contacts -H "Content-Type: application/json" -d "{\"name\":\"Jane Doe\"}"
curl http://localhost:<port>/contacts/<id-from-location-header>
```

## What to observe

- `POST /contacts` returns `201 Created` once `IMessageSender.Send()` has dispatched the command and the in-process handler has added the contact to the repository.
- `GET /contacts/{id}` returns the contact that was created — confirming the command was handled synchronously within the same request/response cycle, even though it travelled through Flowly's messaging pipeline rather than a direct method call.
