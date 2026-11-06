---
paths:
  - "**/*.cs"
---

# .NET Coding Conventions

- **No `Async` suffix on method names.** Do not append `Async` to method names, even when the method is asynchronous. This applies to interface definitions, implementations, and test method names.

## Formatting

### Line Breaks

- I consider a good line width less that 200 characters. There can be scenarios where it is not possible but strive for that.

- When breaking a method being called - not declared - across several lines follow these conventions:

  * One parameter per line.
  * Last parameters ends with the closing paranthesis

  Example:

  ```C#
    someInstace.NotThatWideMethod(parameterOne, parameterTwo);

    someInstace.MethodBeingTooWide(
      parametersOne,
      ParameterTwo,
      ParameterThree,
      ParameterFour,
      ParameterFive);
  ```

- For single line methods use expression bodies structure:

  Example:

  ```C#
    public IMessageBusSender CreateMessageBusSender(string queueName) 
        => _serviceBusSenders.GetOrAdd(queueName, q => new MessageBusSender(serviceBusClient.CreateSender(q)));
  ```

  This also applies when the entire body is a single call delegating to another method (including returning a `Task` without `await`) — do not write a block body with a single `return`/statement.

  Example:

  Not ok:

  ```C#
  public Task IngestForQueue(IDeadLetterReceiver receiver, IReadOnlyCollection<IDeadLetterMessage> messages, string queueName, CancellationToken cancellationToken)
  {
      return Ingest(receiver, messages, queueName, (repository, ct) => repository.SaveBatch(messages, queueName, ct), cancellationToken);
  }
  ```

  Ok:

  ```C#
  public Task IngestForQueue(IDeadLetterReceiver receiver, IReadOnlyCollection<IDeadLetterMessage> messages, string queueName, CancellationToken cancellationToken)
      => Ingest(receiver, messages, queueName, (repository, ct) => repository.SaveBatch(messages, queueName, ct), cancellationToken);
  ```

- Do not unnecessarily do line breaks.

  Example of bad break:

  ```C#
  await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                deadLetterService.Requeue("nonexistent-id"));
  ```

## Parameter names

  - Use type name in camelcase formatting.

  Example:

  ```C#
  public override async Task HandleAsync(DeleteDeadLetterRequest req, CancellationToken ct)
  ```

  Should be

  ```C#
  public override async Task HandleAsync(DeleteDeadLetterRequest deleteDeadLetterRequest, CancellationToken cancellationToken)
  ```

## Brackets

- Always use brackets in for loops

  Example:

  Not ok:

  ```C#
    for(int i = 0; i < 10; i++)
      do.stuff();
  ```

  Ok:

  ```C#
    for(int i = 0; i < 10; i++)
    {
      do.stuff();
    }
  ```

- Always use brackets in `foreach` loops, same as `for` — never a single statement on the same line as the `foreach` header.

  Example:

  Not ok:

  ```C#
    foreach (var settings in queueSettings) await CheckQueue(settings.QueueName, settings.ProviderName, repository, cancellationToken);
  ```

  Ok:

  ```C#
    foreach (var settings in queueSettings)
    {
        await CheckQueue(settings.QueueName, settings.ProviderName, repository, cancellationToken);
    }
  ```

- Always use brackets around a `while` loop body, regardless of what the body is — a single statement, a `try` statement, an `if`, whatever (a single statement is still syntactically "one statement," so it's easy to drop the loop's own braces — don't).

  Example:

  Not ok:

  ```C#
    while (!stoppingToken.IsCancellationRequested)
        try
        {
            var batch = await AccumulateBatch(stoppingToken);
            if (batch.Count == 0) continue;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Batch processing failed");
        }
  ```

  Ok:

  ```C#
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            var batch = await AccumulateBatch(stoppingToken);
            if (batch.Count == 0) continue;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Batch processing failed");
        }
    }
  ```

- In a `do { } while ();` loop, put the `while` clause on its own line under the closing brace — not on the same line as the closing brace.

  Example:

  Not ok:

  ```C#
    do
    {
        batch = await AccumulateBatch(stoppingToken);
    } while (batch.Count > 0);
  ```

  Ok:

  ```C#
    do
    {
        batch = await AccumulateBatch(stoppingToken);
    }
    while (batch.Count > 0);
  ```

## Line Spacing

- Make line spacing to ensure code readability

  Example:

  For methods with two lines of code this is ok:

  ```C#
  public async Task<IMessageBusReceiver> CreateReceiver(string queueName)
    {
        var channel = await connection.CreateChannelAsync();
        return new RabbitMqMessageBusReceiver(channel, queueName);
    }
  ```

  For methods with three lines or more this is not ok:

  ```C#
  public async Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
    {
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false,
            consumerDispatchConcurrency: (ushort)Math.Max(1, options.MaxConcurrentCalls));
        var channel = await connection.CreateChannelAsync(channelOptions);
        return new RabbitMqMessageBusProcessor<TMessage>(channel, queueName, options);
    }
  ```

  It should be:

  ```C#
  public async Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
    {
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false,
            consumerDispatchConcurrency: (ushort)Math.Max(1, options.MaxConcurrentCalls));
        
        var channel = await connection.CreateChannelAsync(channelOptions);
        
        return new RabbitMqMessageBusProcessor<TMessage>(channel, queueName, options);
    }
  ```

## Auto Generated Code

  - Do not uphold these principles for code that is generated by tools like Entity Framework Migrations. Leave such files to themselves.

## Primary Constructors
 
 - Prefer primary constructors for internal classes. Public classes shared with consumers should ensure that fields intended to be private stays private by **not** using primary constructors.


## Order of methods

- Properties always goes after the constructor.