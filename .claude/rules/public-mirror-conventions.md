# Public Mirror Conventions

The canonical repository is a private, self-hosted Gitea instance. A workflow in `.gitea/workflows/` force-pushes `main` and the tags to a public GitHub repository, so **everything tracked in git is public**: all files (including `.claude/`, `.gitea/`, docs, samples, templates), every commit message and the full history. Pull request and issue text stays in Gitea; anything that ends up in a commit message does not.

## Never put these in tracked files, commit messages or pull request descriptions

- Hostnames, domains, IP addresses, ports or URLs of the private infrastructure: the Gitea instance, its machine, runners, internal package feeds or registries, mail servers.
- Local filesystem paths (`/Users/<name>/...`, local NuGet feeds, scratch/temp directories) and machine, network or account names.
- Account names or e-mail addresses other than the author identity already in the package metadata; reviewer identities.
- Secrets or tokens, including example values copied from a real configuration. Workflows refer to secrets and variables by name only.
- The private repository's name or slug where it differs from the public one, and links into the private Gitea instance.
- Gitea issue, pull request or project numbers in files (the GitHub mirror has no matching items, so they point nowhere or at unrelated ones). Describe the thing by name ("capability-aware provider routing") and keep the number in Gitea.

Generic statements are fine ("canonical source is a self-hosted Gitea instance", "tracked in Gitea"). Instructions for agents stay generic too: use the configured `tea` login and the repository's own `origin` remote rather than naming the host or slug.

## Merge commit messages

Gitea's default merge/squash message appends `Reviewed-on: <PR URL>` and `Reviewed-by` (name and e-mail), which would publish the instance hostname. `.gitea/default_merge_message/` holds a deliberately minimal replacement template: do not add `${ReviewedOn}`, `${ReviewedBy}`, `${ClosingIssues}` or `${PullRequestDescription}` (the last two copy `#N` references and closing keywords that GitHub would resolve against its own unrelated issues).

Gitea reads the template from the repository's **default branch** (currently `main`), not the PR target, so the file must exist there for merges into `development` to use it. Keep it identical on both branches and change it on the default branch too. After a merge, check the new commit message for `Reviewed-on`, `Reviewed-by`, `Closes #` lines and copied PR/issue numbers; with a hand-edited message, remove them.

A squash merge can still carry the PR description into the commit body, so PR descriptions contain no closing keywords (`Closes`, `Fixes`, `Resolves` and variants) and no issue or PR numbers. The issue is closed and the card moved in Gitea after the merge.

## Before committing

Search what you are about to commit for the categories above: local paths, private address ranges, personal e-mail domains, and the hostnames and slug from your local `tea` and git configuration:

```bash
host=$(git remote get-url origin | sed -E 's#https?://([^/@]*@)?([^/]+)/.*#\2#')
git diff --cached | grep -inE "/Users/|192\.168\.|\.local\b|@(hotmail|gmail|outlook)|$host"
```

Rewriting published history is a separate, deliberate decision (it changes commit ids that published packages and releases refer to). Never do it as a side effect; raise it with the maintainer.
