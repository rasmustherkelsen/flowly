# Flowly CLI Tool Conventions

## Always install the tool before using it

Any skill or automation that invokes the `flowly` CLI must first ensure the tool is installed. Run this check immediately before the first `flowly` command:

```bash
dotnet tool list --global | grep -q "flowly.tool" || dotnet tool install --global Flowly.Tool
```

The check is idempotent — if the tool is already installed, nothing happens. If the command still fails after install, run:

```bash
dotnet tool update --global Flowly.Tool
```

Then retry the original command once.

## Never reimplement what the tool does

If the `flowly` CLI is unavailable, the correct response is to install it — not to hand-craft the output it would produce (e.g. writing `sbconfig.json` manually). Reimplementing the tool's logic creates drift and bypasses version-specific behaviour.

## Skills must embed the check inline

Skills are deployed to user sites via the `Flowly.Templates` NuGet package and cannot reference files that only exist in this repository. The install check must be written directly in the `SKILL.md` — never as a reference to this rules file or any other repo-local path.

See `.claude/rules/skills-conventions.md` for the full self-contained deployment constraint.
