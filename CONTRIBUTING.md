# Contributing to Flowly

Thank you for your interest in Flowly. This document explains how contributions work.

## Contribution model

Flowly is an opinionated, internally-driven library. **We do not accept unsolicited pull requests.** All contributions must start with an issue — no exceptions.

If you submit a pull request without a linked issue, it will be closed without review.

## Opening an issue first

Before writing any code, [open an issue](../../issues/new/choose) describing:

- What problem you are experiencing or what improvement you are proposing
- Why the existing behaviour does not meet your needs
- Any constraints or context that shapes the solution

We will acknowledge the issue and let you know whether a change is in scope before you invest time in an implementation.

## Branch naming

All work happens on feature branches branched off `main`:

```
feature/<short-description>
```

Examples: `feature/postgres-dead-letters`, `feature/retry-jitter`.

## Pull request expectations

- **One concern per PR.** A PR that fixes a bug should fix that bug, not also refactor unrelated code.
- **Small and reviewable.** Prefer several focused PRs over one large one.
- **Every PR must close an issue.** Add `Closes #<issue>` in the PR description.
- **Tests included.** New behaviour requires new tests. Existing tests must continue to pass.
- **Documentation updated.** If you change anything user-facing, update the relevant docs in `docs/` and the root `README.md`.

## Code style (very short version)

- .NET 10, nullable enabled, implicit usings
- No `Async` suffix on method names
- Primary constructors for internal classes; traditional constructors for public API types
- No comments — names must be self-explanatory
- XML doc on all public and protected members

Run `dotnet build` and `dotnet test` before submitting. Both must pass cleanly.

## Licence

By submitting a contribution you agree that your code will be licensed under the same licence as the rest of the project.
