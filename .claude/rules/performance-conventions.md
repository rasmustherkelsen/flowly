# Performance Conventions

- If a method contains multiple await statements examine if they can be executed in parallel using Task.WhenAll for enhanced IO throughput.

- Keep memory allocation low. It is important that code is great for reading but do not keep the code unnecessarily verbose as memory and allocation is a concern in Flowly usage scenarios.

