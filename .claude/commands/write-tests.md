Write unit tests for the code the user has specified or currently has in context.

1. Read the source file(s).
2. Identify the public API surface to test.
3. Locate or create the corresponding `.Tests` project (add to `Flowly.sln` if new).
4. Write tests covering the happy path and key edge cases.
5. Run `dotnet test --filter "FullyQualifiedName~{ClassName}Tests"` to verify they pass.
