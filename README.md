# hevy-mcp

Use your Hevy workout data from Codex, Claude, or any other MCP client.

Ask questions such as:

- “What did I train last week?”
- “How has my squat volume changed?”
- “Find my push-day routines.”
- “Create a completed workout from this routine.”

Your MCP client can search routines and exercises, inspect workout history, summarize training, and manage workouts, routines, custom exercises, and body measurements.

## Get started

You need [Docker](https://www.docker.com/products/docker-desktop/), a [Hevy API key](https://hevy.com/settings?developer), and either Codex or Claude Code.

Run the setup assistant with Node.js:

```sh
npx --yes github:imhalawa/hevy-mcp setup
```

Or with Bun:

```sh
bunx --bun --package github:imhalawa/hevy-mcp hevy-mcp setup
```

It asks for your API key without displaying it, saves it in a user-only file, pulls the Docker image, and configures every installed supported client. Read-only access is the default; the assistant asks before enabling writes.

The setup registers this server as `hevy-mcp`; an existing MCP server named `hevy` is left untouched. In WSL, use a Linux installation of Node.js or Bun rather than the Windows executable exposed through `/mnt/c`.

Restart Codex or Claude Code, then try:

> Show my five most recent Hevy workouts.

To remove this server from every detected client and delete its saved API key:

```sh
npx --yes github:imhalawa/hevy-mcp uninstall
```

Uninstall leaves other MCP registrations and the cached Docker image untouched.

The same command works on Windows, macOS, and Linux. Run it again to rotate the API key or change write access.

## Keep writes under control

The setup assistant disables write tools unless you explicitly enable them. Write tools also support `dry_run`, so the client can validate the exact change before sending it to Hevy.

The server runs locally, stores no fitness data, and sends no telemetry. Your API key is passed to the container at startup and is never built into the image.

## More

- [Architecture](docs/architecture.md)
- [Security](SECURITY.md)
- [Release verification](docs/release-verification.md)
- [Contributing](CONTRIBUTING.md)
- [MIT License](LICENSE)
