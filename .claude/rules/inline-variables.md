# Inline Single-Use Variables

Do not declare a variable solely to pass it to a method once. If a value is used only in a single method call, inline it directly in that call.

**Bad:**
```csharp
var channelOptions = new CreateChannelOptions(
    publisherConfirmationsEnabled: false,
    publisherConfirmationTrackingEnabled: false);

var channel = await connection.CreateChannelAsync(channelOptions);
```

**Good:**
```csharp
var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
    publisherConfirmationsEnabled: false,
    publisherConfirmationTrackingEnabled: false));
```

**Bad:**
```csharp
var queueName = resolver.Resolve(typeof(MyMessage));
sender.Send(queueName, message);
```

**Good:**
```csharp
sender.Send(resolver.Resolve(typeof(MyMessage)), message);
```

## Exceptions

Extract to a variable when any of the following apply:

- The value is used more than once.
- The variable name adds meaning that cannot be inferred from the expression alone (e.g. a complex boolean condition that benefits from a descriptive name).
- Inlining would push the line past the 200-character limit in a way that cannot be resolved by other formatting means.
- The expression has a side effect and extracting it makes the side effect order explicit and obvious.
