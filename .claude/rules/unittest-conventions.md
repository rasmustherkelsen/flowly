# Unit Test Conventions

- Test should structured so there is a test library per project that needs test.

  Example:

  | Project            | Unit Test Project        |
  | ------------------ | ------------------------ |
  | Flowly             | Flowly.Tests             |
  | Flowly.DeadLetters | Flowly.DeadLetters.Tests |

- Use xUnit