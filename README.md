# hevy-mcp

Use your Hevy workout data from Codex, Claude, or any other MCP client.

Ask questions such as:

- “What did I train last week?”
- “How has my squat volume changed?”
- “Find my push-day routines.”
- “Create a completed workout from this routine.”

Your MCP client can search routines and exercises, inspect workout history, summarize training, and manage workouts, routines, custom exercises, and body measurements.

## Get started

You need [Docker](https://www.docker.com/products/docker-desktop/), a [Hevy API key](https://hevy.com/settings?developer), and an MCP client.

Pull the image:

```powershell
docker pull ghcr.io/imhalawa/hevy-mcp:0.1.1
```

Set your API key for the current PowerShell session without displaying it:

```powershell
$key = Read-Host 'Hevy API key' -AsSecureString
$env:HEVY_API_KEY = [System.Net.NetworkCredential]::new('', $key).Password
```

Add the server to Codex:

```powershell
codex mcp add hevy -- docker run --rm -i --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m -e HEVY_API_KEY ghcr.io/imhalawa/hevy-mcp:0.1.1
```

Restart Codex, then try:

> Show my five most recent Hevy workouts.

The first request may take a moment while Docker starts the container.

### macOS and Linux

```sh
read -r -s -p "Hevy API key: " HEVY_API_KEY && export HEVY_API_KEY && printf '\n'
docker pull ghcr.io/imhalawa/hevy-mcp:0.1.1
codex mcp add hevy -- docker run --rm -i --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m -e HEVY_API_KEY ghcr.io/imhalawa/hevy-mcp:0.1.1
```

Other stdio MCP clients use the same `docker run` command. Make sure the client inherits `HEVY_API_KEY` from its environment.

## Keep writes under control

Run in read-only mode until you want the client to change Hevy data:

```powershell
codex mcp add hevy-readonly -- docker run --rm -i --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m -e HEVY_API_KEY -e HEVY_READ_ONLY=true ghcr.io/imhalawa/hevy-mcp:0.1.1
```

Write tools also support `dry_run`, so the client can validate the exact change before sending it to Hevy.

The server runs locally, stores no fitness data, and sends no telemetry. Your API key is passed to the container at startup and is never built into the image.

## More

- [Architecture](docs/architecture.md)
- [Security](SECURITY.md)
- [Release verification](docs/release-verification.md)
- [Contributing](CONTRIBUTING.md)
- [MIT License](LICENSE)
