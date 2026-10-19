# Local Environment Conventions

Agents work on a developer machine that also runs unrelated containers, databases and tools. Only ever clean up what you created yourself.

## Docker

- Give every container, network and volume you create a unique name and a label that identifies the task, for example `docker run --name flowly-<task>-rmq --label flowly-agent=<task> ...`, and use named volumes you created yourself.
- Images often declare anonymous volumes (RabbitMQ and database images do). Remove containers with `docker rm -fv <name>` so the anonymous volumes go with them; plain `docker rm -f` leaves them behind.
- Clean up by name or by label only:
  - `docker rm -fv <name>`, `docker volume rm <name>`, `docker network rm <name>`
  - everything labelled for your task: `docker ps -aq --filter label=flowly-agent=<task> | xargs docker rm -fv` (the `docker ps` part only lists ids; the `xargs` part removes them)
- Never run any `docker ... prune` command (`docker system prune`, `docker volume prune`, `docker container prune`, `docker network prune`, `docker image prune`, with or without `-f` or `-a`). They delete resources that belong to other work, and unused volumes can hold data that cannot be recreated.
- Never stop, restart or remove a container you did not start. Do not reuse occupied ports: publish to a free ephemeral host port, for example `-p 127.0.0.1::5672`, and find it with `docker port <name> 5672`.
- Docker Compose: the sample compose files in this repository use fixed `container_name` entries and fixed host ports that can collide with other work. Run them under a project name of your own (`docker compose -p flowly-<task> ...`) from a copy in your scratch directory with the names and ports changed, and only run `docker compose -p flowly-<task> down -v` for a project you started.

## Global tools

Do not change global tool configuration (git config, NuGet sources, shell profiles, other `dotnet` global tools) to make a task work; use a command-line option or a file in your scratch directory instead. The one exception is the Flowly CLI itself: installing or updating it with `dotnet tool install --global Flowly.Tool` / `dotnet tool update --global Flowly.Tool` is required by `flowly-tool-conventions.md` before any `flowly` command.

## Other shared state

- Keep throwaway files in the session scratch or temp directory, not in the repository, and delete only the files you created.
- Only touch branches and worktrees that you created yourself. Never check out, reset, rebase or delete a branch or worktree that belongs to another agent or to the maintainer.
