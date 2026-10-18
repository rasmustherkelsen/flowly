# Bug Reporting Conventions

If you discover a bug that is not related to the work at hand, do not fix it as a side effect of the current change and do not silently ignore it. Create a new issue in Gitea labeled `bug`, then go straight back to the task you were doing: the point is to keep the current change focused and let the bug be picked up later.

## What to include

- A title naming the symptom, not the suspected cause.
- Where it was found: file and line, plus the conditions that trigger it.
- What happens versus what should happen.
- A minimal reproduction or failing scenario when one is known.
- Enough context that someone who has never seen the current work can pick it up. Do not reference session-local material such as plan files or task labels.

## How

First check that it has not been reported already, including closed issues (a closed match may be a regression, which is worth saying in a new issue):

```bash
tea issues list --state all --labels bug --keyword "<distinctive words from the symptom>"
```

If an open issue already covers it, add the new details as a comment (`tea comment <number> "<text>"`) instead of filing a duplicate. Otherwise use the `tea` CLI, which needs no host or repository flags inside this repository (see `project-management-conventions.md`):

```bash
tea issues create --labels bug --title "<symptom>" --description "<body>"
```

The `bug` label should already exist. If `tea labels list` does not show it, create it first with `tea labels create --name bug --color d73a4a --description "Something isn't working"`.

Mention the issue number (or the existing issue you commented on) in your report to the user so it is not lost. Issue text stays in Gitea, but this repository is mirrored publicly (see `public-mirror-conventions.md`), so keep secrets and private infrastructure details out of it, and do not put the issue number into tracked files or commit messages.

## What this does not cover

A bug in the code you are already changing, or one that blocks the current task, is part of the work at hand: fix it as part of the change. Open questions and design follow-ups are not bugs; track those through the normal issue workflow without the `bug` label.
