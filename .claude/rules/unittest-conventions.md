# Unit Test Conventions

- Tests should be structured so there is a test library per project that needs tests.

  Example:

  | Project            | Unit Test Project        |
  | ------------------ | ------------------------ |
  | Flowly             | Flowly.Tests             |
  | Flowly.DeadLetters | Flowly.DeadLetters.Tests |

- Location

  Tests are located in the /tests root subfolder

  Tests are added to the Flowly solution file

- Amount of tests

  Ideally there should be a test file per file in the real production code testing the content of that file. Of course it does not make sense to test *everything* but there should be a coverage of no less that 70%

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

- **Never** use timing like Task.Delay to wait on something to happen. It is error prune might lead to false negatives.