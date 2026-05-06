# General documentation conventions

- Always update documentation for AI usage

## End-user documentation

End-user documentation must be kept current at all times. Whenever a feature is added, changed, or removed, update the relevant user-facing docs in the same change.

End-user documentation lives in:
- `docs/` — GitHub Pages site and general guides
- `README.md` files under `Samples/` — sample-specific instructions
- `Samples/README.md` — samples index

When implementing a change, ask: *does this affect what an operator or developer needs to know to configure or use Flowly correctly?* If yes, update the docs before considering the task done. This includes:

- New configuration options or registration methods
- Changed behaviour of existing options (e.g. constraints, defaults, failure modes)
- Startup validation errors a user might encounter
- Any prerequisite infrastructure a user must provision manually

## Code Comments

- All public and protected methods, field and properties in public classes or interfaces must be documented using XML documentation. Including parameters and return type preferably with a ref to the type being referenced. Be thourough so that the documentation is precise. The user should have a good experience using intellisense with the library. If existing information is provided please check if it still seems to convey the precise information or if it is not good enough or the underlying code has changed so it is plain wrong.

## Readme file in root

- Always ensure the README.md in the root of the repo is kept up-to-date. This is the information github users encounter when they navigate to flowly there.