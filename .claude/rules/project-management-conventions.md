# Project Management Conventions

Project management happens in the repository's Gitea instance, not in the repository. The repository holds code, documentation of how things work, and design records. It does not hold work tracking.

## What belongs in Gitea

- **Issues** for every unit of work that is not finished in the current change: bugs (label `bug`, see `bug-reporting-conventions.md`), features, follow-ups, deferred items, open design questions, release tasks.
- **Projects** (a board with `To do` / `In progress` / `Done` columns) for efforts that span several issues or pull requests. Add the pull requests that carry the work to the board as well.
- **Labels** to group related issues (for example `stream-only-transport`, `message-streams`). Reuse an existing label before creating one.

Before starting multi-step work, look for an existing project or issues (`tea issues list`, and the projects API below) and attach to them rather than starting a parallel list.

The repository is mirrored publicly (see `public-mirror-conventions.md`), so none of this tracking may leak into files: do not write Gitea issue, pull request or project numbers or URLs into tracked files, and keep hostnames and the repository slug out of them as well. Refer to work by name.

## What must not be in the repository

Do not add, or keep growing, any of the following in markdown files, rules, skills, proposals, `docs/ai/CONTEXT.md`, or code comments:

- status lines ("Implemented", "Phases 0-2 done", "Not started"), progress markers, or checklists of work still to do
- roadmap, backlog, sequencing/phase tables that track delivery, or "follow-ups", "deferred", "planned", "future work" lists
- TODO files or todo folders

Scaffold placeholders inside skills and templates (for example `// TODO: implement handler logic` in generated user code) are not project management and are fine.

If a document needs to say that something is not supported or intentionally limited, state it as a fact about current behaviour ("X is not supported") without a delivery plan. If something was deferred, file an issue for it; in the file, at most say that it is tracked in Gitea, without a number or URL.

## Proposals and design documents

`docs/proposals/` holds design records: the problem, the options considered, the decision and its rationale, compatibility analysis, and technical caveats or unverified assumptions. They must not carry delivery tracking. When a design implies work, create the issues in Gitea (and a project when there are several), linking them to each other there; do not keep a status, sequencing or follow-up section in the document. Update the document when a design decision changes, not when work progresses.

`docs/ai/CONTEXT.md` and the other onboarding docs describe how the code behaves now. They never say what is planned.

## Working notes during a session

A plan or scratch list for the current session stays outside the repository (the session plan or scratchpad) and is never committed. It is not an artifact other people can see, so do not cite it in commits or pull requests. Anything that must outlive the session becomes a Gitea issue.

## How to do it with `tea`

The `tea` CLI is installed and logged in to the instance, and inside this repository it works out the instance and repository from the `origin` remote, so the commands need no host, login or repository flags. Only if it cannot find the repository, add `--login <login> --repo <owner>/<repo>` (`tea login list` shows the login); never write those values into tracked files.

```bash
tea issues create --labels <label> --title "<title>" --description "$(cat body.md)"
tea labels create --name <name> --color <hex> --description "<text>"
tea pulls create --base development --head feature/<slug> --title "<title>" --description "$(cat body.md)"
```

Projects have no `tea` command; use `tea api`, which fills in `{owner}` and `{repo}` itself. The instance serves its API schema at `/swagger.v1.json`.

```bash
tea api -X POST '/repos/{owner}/{repo}/projects' -f title="<title>" -f description="<text>"    # returns the project id
tea api -X POST '/repos/{owner}/{repo}/projects/<id>/columns' -f title="To do"                 # repeat for "In progress" and "Done"
tea api '/repos/{owner}/{repo}/issues/<number>'                                                # read the issue's global "id"
tea api -X POST '/repos/{owner}/{repo}/projects/<id>/columns/<column_id>/issues/<global_issue_id>'   # add an issue or pull request to a column
```

The last call takes the issue's **global** `id`, not its number. Always read it from the **issues** endpoint, for pull requests as well (`tea api '/repos/{owner}/{repo}/issues/<pr number>'`): the `id` in the `/pulls/<number>` response is a different id and puts an unrelated old card on the board. A newly created project has no columns, so create them first. Move a card by adding it to another column, and keep the board current as work progresses: when work starts, the card goes to `In progress`; when the pull request merges, `Done`.

## Writing issues

- Name the symptom or the goal in the title, say where it applies, and give enough context for someone who has not seen the current work. Do not reference session-local material such as plan files or task labels.
- Link the design document and related issues or pull requests (inside Gitea, numbers are fine). Issue and pull request text stays in Gitea; the merge message template does not copy it into commit messages.
- For an open design question, list the options and state the question; do not decide it in the issue. This repository's rule for the streaming area is to ask rather than assume, and the same applies when filing the issue: raise the question with the maintainer before settling it.
- Pull request descriptions name the work in prose and carry no closing keywords (`Closes`, `Fixes`, `Resolves`) and no issue or pull request numbers, because a squash merge can copy the description into the commit message (see `public-mirror-conventions.md`). Close the issue and move the project card to `Done` in Gitea once the pull request is merged; an agent that opened the pull request leaves that to the maintainer's session and names the issue in its report instead.
- No Claude or Anthropic attribution lines in issue or pull request text.
