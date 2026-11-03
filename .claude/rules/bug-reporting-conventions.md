# Bug Reporting Conventions

If you discover a bug unrelated to the work at hand, do not fix it as a side effect and do not silently ignore it. File a Gitea issue labeled `bug`, then go straight back to the task, so the current change stays focused.

## What to include

- A title naming the symptom, not the suspected cause.
- Where it was found (file and line) and the conditions that trigger it.
- What happens versus what should happen, with a minimal reproduction when known.
- Enough context for someone who has never seen the current work; no session-local material such as plan files or task labels.

## How

First check it has not been reported, including closed issues (a closed match may be a regression, worth saying in a new issue):

```bash
tea issues list --state all --labels bug --keyword "<distinctive words from the symptom>"
```

If an open issue covers it, add the details as a comment (`tea comment <number> "<text>"`) instead of filing a duplicate. Otherwise (`tea` needs no host or repository flags here, see `project-management-conventions.md`):

```bash
tea issues create --labels bug --title "<symptom>" --description "<body>"
```

If `tea labels list` lacks the `bug` label, create it: `tea labels create --name bug --color d73a4a --description "Something isn't working"`.

Mention the issue number (or the one you commented on) in your report to the user. The repository is mirrored publicly (see `public-mirror-conventions.md`): keep secrets and private infrastructure details out of the issue, and do not put the number into tracked files or commit messages.

## What this does not cover

A bug in code you are already changing, or one that blocks the current task, is part of the work: fix it. Open questions and design follow-ups are not bugs; track them through the normal issue workflow without the `bug` label.
