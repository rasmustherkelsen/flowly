# Public Mirror Conventions

The canonical repository lives in a private, self-hosted Gitea instance. A workflow in `.gitea/workflows/` force-pushes `main` and the tags to a public GitHub repository, so **everything tracked in git is public**: all files (including `.claude/`, `.gitea/`, docs, samples and templates), every commit message and the full history. Pull request and issue text stays in Gitea, but anything that ends up in a commit message does not.

## Never put these in tracked files, commit messages or pull request descriptions

- Hostnames, domains, IP addresses, ports or URLs of the private infrastructure: the Gitea instance, the machine it runs on, runners, internal package feeds or registries, mail servers.
- Local filesystem paths (`/Users/<name>/...`, local NuGet feeds, scratch or temp directories) and machine, network or account names.
- Account names or e-mail addresses other than the author identity already published in the package metadata; reviewer identities.
- Secrets or tokens of any kind, including example values copied from a real configuration. Workflows refer to secrets and variables by name only.
- The private repository's name or slug where it differs from the public one, and links into the private Gitea instance (issue, pull request or project URLs).
- Gitea issue, pull request or project numbers in files. The GitHub mirror has no matching issues, so the numbers point nowhere or at unrelated GitHub items. Describe the thing by name instead ("capability-aware provider routing") and keep the number in Gitea.

Generic statements are fine ("canonical source is a self-hosted Gitea instance", "tracked in Gitea"). Instructions for agents must stay generic too: tell them to use the configured `tea` login and the repository's own `origin` remote rather than naming the host or the slug.

## Merge commit messages

Gitea's default merge and squash messages append a `Reviewed-on: <pull request URL>` line (and `Reviewed-by` with the reviewer's name and e-mail). That would publish the instance hostname with every merge. `.gitea/default_merge_message/` holds the merge message template that replaces the default. It is deliberately minimal: do not add `${ReviewedOn}`, `${ReviewedBy}`, `${ClosingIssues}` or `${PullRequestDescription}` to it (the last two would copy `#N` references and closing keywords, which GitHub would resolve against its own unrelated issues). If a merge is done with a hand-edited message, remove those lines.

## Before committing

Search what you are about to commit for the categories above: local paths, private address ranges, personal e-mail domains, and the hostnames and repository slug from your local `tea` and git configuration, for example:

```bash
host=$(git remote get-url origin | sed -E 's#https?://([^/@]*@)?([^/]+)/.*#\2#')
git diff --cached | grep -inE "/Users/|192\.168\.|\.local\b|@(hotmail|gmail|outlook)|$host"
```

Rewriting published history is a separate, deliberate decision (it would change commit ids that published packages and releases refer to). Do not do it as a side effect of other work; raise it with the maintainer.
