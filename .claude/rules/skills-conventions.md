# Skills Conventions

Claude Code skills live in `.claude/skills/`. Each skill is a directory containing a `SKILL.md` file.

## Keeping skills current

Skills must be updated in the same change as the code they describe. When implementing a feature, ask: *does this affect what a developer needs to know to use Flowly correctly from an AI assistant?* If yes, update the relevant skill before considering the task done.

Common triggers:

- A new registration method is added (e.g. a new transport or backend package) → update `flowly-setup-azure-service-bus/SKILL.md` and any other setup skills
- Handler base classes gain new capabilities or new configuration options → update `create-message-handler/SKILL.md`
- `RecurringJobHandler` or `[RecurringJob]` behaviour changes → update `create-recurring-job/SKILL.md`
- The contracts project pattern or `IJobMessage` contract changes → update `create-contracts-assembly/SKILL.md`
- A previously documented step becomes wrong, redundant, or replaced by a better approach → fix or remove it

## Adding new skills

When adding a significant new feature that a developer would scaffold repeatedly, consider adding a skill. Follow the existing structure: one directory per skill, `SKILL.md` inside, frontmatter with `description` and `arguments` if the skill accepts parameters.

Naming convention:
- Generic skills (applicable to any project using Flowly): flat name, e.g. `create-message-handler`
- Flowly-specific or library-specific skills: prefixed with `flowly-`, e.g. `flowly-setup-azure-service-bus`
