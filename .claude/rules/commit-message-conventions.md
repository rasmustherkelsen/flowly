# Commit Message Conventions

Write all commit messages following the [Conventional Commits](https://www.conventionalcommits.org/) specification.

## Format

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

## Types

Use one of: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`.

- `feat` — a new feature (e.g. a new registration method, handler type, template flag)
- `fix` — a bug fix
- `docs` — documentation-only changes
- `refactor` — code change that neither fixes a bug nor adds a feature
- `chore` — tooling, dependency bumps, repo maintenance
- `test` — adding or correcting tests
- `ci` — CI/workflow configuration changes
- `build` — build system or packaging changes

## Scope

Optional, in parentheses after the type, naming the affected project or area (e.g. `feat(templates):`, `fix(rabbitmq):`, `docs(readme):`). Omit the scope when the change spans many areas.

## Description

Imperative mood, lowercase, no trailing period: `add`, not `Added` or `adds`.

## Body

Explain *why*, not *what* — the diff already shows what changed. Wrap at ~72 characters. Separate from the subject line with a blank line.

## Breaking changes

Mark with `!` after the type/scope (`feat(templates)!: ...`) and/or a `BREAKING CHANGE:` footer describing the impact and migration path.

## Example

```
feat(templates): add --stream/--partitions to flowlyapp and flowlyaspireapp

Wires [StreamPartitions(n)] and a partition-key sender example into the
scaffolded templates, adds RabbitMQ stream plugin/port config, and adds
a full partitioned-stream sample under samples/RabbitMQ.
```
