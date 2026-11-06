# Project Management Conventions

Project management happens in the repository's Gitea instance, not in the repository. The repository holds code, documentation of how things work, and design records.

## What belongs in Gitea

- **Issues** for every unit of work not finished in the current change: bugs (label `bug`, see `bug-reporting-conventions.md`), features, follow-ups, deferred items, open design questions, release tasks.
- **Projects** (board with `To do` / `In progress` / `Done`) for efforts spanning several issues or pull requests; add the pull requests to the board too.
- **Labels** to group related issues (e.g. `stream-only-transport`, `message-streams`). Reuse an existing label before creating one.

Before starting multi-step work, look for an existing project or issues (`tea issues list`, the projects API below) and attach to them.

The repository is mirrored publicly (see `public-mirror-conventions.md`): no Gitea issue, pull request or project numbers or URLs, hostnames or repository slug in tracked files. Refer to work by name.

## What must not be in the repository

In markdown files, rules, skills, proposals, `docs/ai/CONTEXT.md` or code comments, do not add or grow:

- status lines ("Implemented", "Phases 0-2 done"), progress markers, checklists of work still to do
- roadmaps, backlogs, delivery-tracking phase tables, "follow-ups" / "deferred" / "planned" / "future work" lists
- TODO files or folders

Scaffold placeholders in skills and templates (e.g. `// TODO: implement handler logic` in generated user code) are fine. If a document must say something is unsupported or limited, state it as a fact about current behaviour without a delivery plan; deferred work becomes an issue (at most "tracked in Gitea", no number or URL).

## Proposals and design documents

`docs/proposals/` holds design records: problem, options considered, decision and rationale, compatibility analysis, caveats and unverified assumptions. No delivery tracking; when a design implies work, create the issues (and a project for several) in Gitea and link them there. Update a document when a design decision changes, not when work progresses. `docs/ai/CONTEXT.md` and other onboarding docs describe how the code behaves now, never what is planned.

## Working notes during a session

Plans and scratch lists stay outside the repository and are never committed. They are not visible to others, so do not cite them in commits or pull requests; anything that must outlive the session becomes a Gitea issue.

## How to do it with `tea`

`tea` is logged in and works out the instance and repository from the `origin` remote, so no host, login or repository flags are needed. Only if it cannot find the repository, add `--login <login> --repo <owner>/<repo>` (`tea login list`); never write those values into tracked files.

```bash
tea issues create --labels <label> --title "<title>" --description "$(cat body.md)"
tea labels create --name <name> --color <hex> --description "<text>"
tea pulls create --base development --head feature/<slug> --title "<title>" --description "$(cat body.md)"
```

Projects have no `tea` command; use `tea api` (fills in `{owner}` and `{repo}`; the schema is at `/swagger.v1.json`):

```bash
tea api -X POST '/repos/{owner}/{repo}/projects' -f title="<title>" -f description="<text>"    # returns the project id
tea api -X POST '/repos/{owner}/{repo}/projects/<id>/columns' -f title="To do"                 # repeat for "In progress" and "Done"
tea api '/repos/{owner}/{repo}/issues/<number>'                                                # read the issue's global "id"
tea api -X POST '/repos/{owner}/{repo}/projects/<id>/columns/<column_id>/issues/<global_issue_id>'   # add an issue or PR to a column
```

The last call takes the issue's **global** `id`, not its number. Always read it from the **issues** endpoint, for pull requests too (the `id` in `/pulls/<number>` is a different id and puts an unrelated old card on the board). A new project has no columns; create them first. Move a card by adding it to another column and keep the board current: `In progress` when work starts, `Done` when the pull request merges.

## Writing issues

- Name the symptom or goal in the title, say where it applies, give enough context for someone who has not seen the current work. No session-local material such as plan files or task labels.
- Link the design document and related issues or pull requests (numbers are fine inside Gitea). Issue and PR text stays in Gitea; the merge message template does not copy it into commits.
- For an open design question, list the options and state the question; do not decide it in the issue. In the streaming area, ask the maintainer before settling it.
- Pull request descriptions name the work in prose with no closing keywords (`Closes`, `Fixes`, `Resolves`) and no issue or pull request numbers, because a squash merge can copy the description into the commit message. Close the issue and move the card to `Done` after the merge; an agent that opened the PR leaves that to the maintainer's session and names the issue in its report.
- No Claude or Anthropic attribution lines in issue or pull request text.
