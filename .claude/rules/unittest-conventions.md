# Unit Test Conventions

- Tests should be structured so there is a test library per project that needs tests.

  Example:

  | Project            | Unit Test Project        |
  | ------------------ | ------------------------ |
  | Flowly             | Flowly.Tests             |
  | Flowly.DeadLetters | Flowly.DeadLetters.Tests |

- Use xUnit

- Name the system under test as a normal instance using the type name.

  Example:

  ```C#
  var mockOrder = new MockOrder();
  var purchase = new Purchase(mockOrder);

  purchase.ValidateOrders();

  Assert.True(purchase.CanBeShipped);
  ```

  - Do **NOT** name the system under test variable as "sut"
  - Do **NOT** use generic names like 'service' or 'repository'

- When fakes implement a shared interface or could be useful across multiple test classes, extract them into dedicated classes in a `Fakes` folder within the test project, under the namespace `{TestProject}.Fakes`.

  - One class per file, named after the type it fakes (e.g. `FakeMessageBusClient`)
  - Keep fakes that are only used by a single test class as private nested classes on that test class
