# Local Environment Conventions

Agents work on a developer machine that also runs unrelated containers, databases and tools. Only ever clean up what you created yourself.

## Docker

- Give every container, network and volume you create a unique name and a task label (`docker run --name flowly-<task>-rmq --label flowly-agent=<task> ...`), and use named volumes you created.
- Images often declare anonymous volumes (RabbitMQ and database images do): remove containers with `docker rm -fv <name>`; plain `docker rm -f` leaves the volumes behind.
- Clean up by name or label only: `docker rm -fv <name>`, `docker volume rm <name>`, `docker network rm <name>`, or everything labelled for your task: `docker ps -aq --filter label=flowly-agent=<task> | xargs docker rm -fv`.
- **Never run any `docker ... prune`** (system, volume, container, network, image; with or without `-f`/`-a`): it deletes other work's resources, and unused volumes can hold data that cannot be recreated.
- Never stop, restart or remove a container you did not start. Do not reuse occupied ports: publish to a free ephemeral host port (`-p 127.0.0.1::5672`) and find it with `docker port <name> 5672`.
- Docker Compose: the repository's sample compose files use fixed `container_name`s and host ports that can collide. Run a copy from your scratch directory with names and ports changed, under your own project name (`docker compose -p flowly-<task> ...`), and only run `docker compose -p flowly-<task> down -v` for a project you started.

## Global tools

Do not change global tool configuration (git config, NuGet sources, shell profiles, other `dotnet` global tools); use a command-line option or a file in your scratch directory. The one exception is the Flowly CLI: installing or updating it with `dotnet tool install|update --global Flowly.Tool` is required by `flowly-tool-conventions.md` before any `flowly` command.

## Other shared state

- Keep throwaway files in the session scratch or temp directory, not the repository, and delete only files you created.
- Only touch branches and worktrees you created. Never check out, reset, rebase or delete one that belongs to another agent or the maintainer.
