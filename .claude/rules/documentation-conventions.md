# General documentation conventions

- Always update documentation for AI usage

## End-user documentation

End-user documentation must be kept current at all times. Whenever a feature is added, changed, or removed, update the relevant user-facing docs in the same change.

End-user documentation lives in:
- `README.md` — root readme; the first thing GitHub visitors see
- `docs/README.md` — GitHub Pages landing page (table of contents; links to `README.md` as the full reference)
- `docs/ai/CONTEXT.md` — AI/contributor onboarding; must always reflect the full set of available registration methods and packages
- `docs/` — other guides (quickstarts, transport-specific docs)
- `README.md` files under `Samples/` — sample-specific instructions
- `Samples/README.md` — samples index
- `.claude/CLAUDE.md` — project structure section

When implementing a change, ask: *does this affect what an operator or developer needs to know to configure or use Flowly correctly?* If yes, update the docs before considering the task done. This includes:

- New configuration options or registration methods
- Changed behaviour of existing options (e.g. constraints, defaults, failure modes)
- Startup validation errors a user might encounter
- Any prerequisite infrastructure a user must provision manually

### Adding a new backend package

Adding a new database backend package (e.g. a new provider for `Flowly.Jobs` or `Flowly.DeadLetters`) is a canonical example where **all** documentation locations must be updated in the same change:

1. `README.md` — add to the packages table and installation section
2. `docs/ai/CONTEXT.md` — add the new registration method(s) to the relevant section (Job State Tracking, Dead Letter Tracking, etc.)
3. `.claude/CLAUDE.md` — add the new project to the Project Structure list

The task is **not done** until all four are updated.

### Adding, changing, or removing an attribute

`docs/attributes-reference.md` is the single consolidated index of every Flowly attribute (handler-class, message/event contract, and job-specific). Whenever an attribute is added, its constructor/parameters change, its default changes, or it's removed, update **all** of the following in the same change:

1. `docs/attributes-reference.md` — add/update/remove the row in the relevant table (Handler-class / Message & event contract / Job-specific), including purpose, applies-to, and default
2. The attribute's XML doc comments on the `Attribute` class itself
3. Its existing detailed section (e.g. `README.md` "Queue configuration attributes", `docs/ai/CONTEXT.md` "Handler-level queue attributes", or the attribute's own feature section) — these existing tables/prose are kept in parallel with the appendix, not replaced by it
4. Any `SKILL.md` under `.claude/skills/` that references the attribute (e.g. `create-message-handler`, `create-batch-handler`, `create-recurring-job`)

The task is **not done** until `docs/attributes-reference.md` and the other affected locations agree.

## Code Comments

- All public and protected methods, field and properties in public classes or interfaces must be documented using XML documentation. Including parameters and return type preferably with a ref to the type being referenced. Be thourough so that the documentation is precise. The user should have a good experience using intellisense with the library. If existing information is provided please check if it still seems to convey the precise information or if it is not good enough or the underlying code has changed so it is plain wrong.

## Readme file in root

- Always ensure the README.md in the root of the repo is kept up-to-date. This is the information github users encounter when they navigate to flowly there.