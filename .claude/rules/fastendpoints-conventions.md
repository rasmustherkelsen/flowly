# FastEndpoints Conventions

## Dependency Injection

Use C# primary constructor injection in `Endpoint<>` classes. Do not use public property injection (`public IFoo Foo { get; set; } = null!`).

Example:

```csharp
class MyEndpoint(IFooService fooService) : Endpoint<MyEndpoint.MyRequest>
{
    public override void Configure() { ... }

    public override async Task HandleAsync(MyRequest req, CancellationToken ct)
    {
        await fooService.DoSomething(ct);
    }
}
```
