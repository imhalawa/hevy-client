# Security policy

## Supported versions

Security fixes are made on the default branch and the latest released semantic-version line. Older releases may not receive fixes. Container users should pin a reviewed version or digest and subscribe to repository security advisories.

## Report a vulnerability privately

Use the repository's **Security → Report a vulnerability** form to open a private GitHub Security Advisory. Include the affected version or image digest, impact, minimal reproduction, and any suggested mitigation. Do not include a real Hevy API key, MCP bearer token, workout payload, activity timestamp, or body measurement.

If private reporting is unavailable, open a public issue containing no exploit or sensitive user data and ask a maintainer to establish a private channel. Do not publish a working exploit before a fix is available. Maintainers will acknowledge the report, assess scope, coordinate remediation, and credit the reporter when desired; response times are best effort for this volunteer project.

For ordinary bugs without sensitive security impact, use the public issue tracker and attach only the allowlisted output of `get_diagnostics` or already-redacted stderr records.

## Threat model

`hevy-client` assumes the machine account running Docker, the Docker daemon administrator, and the configured MCP client are trusted with access to the selected Hevy account. A local administrator or a client allowed to invoke write tools can act with those privileges. The image cannot protect a secret from a compromised host or Docker daemon.

Security boundaries provided by the project include:

- The Hevy key comes only from `HEVY_API_KEY`, is never a tool argument, and is sent only to the fixed HTTPS Hevy origin.
- Local stdio mode opens no port and is the recommended deployment.
- HTTP mode requires a distinct bearer token, explicit trusted hosts, safe Origin matching, and an operator-provided TLS reverse proxy.
- Read-only mode omits write tools; mutation tools otherwise expose dry runs and replacement guards.
- The chiseled image has no shell or package manager, runs as UID 1654 (`app`), and supports a read-only root filesystem.
- Logs and diagnostics use typed allowlists and are off by default.
- No user data or credentials are persisted by the application and no telemetry is uploaded.

Out of scope are a compromised MCP client or host, a malicious Docker daemon administrator, security properties of the upstream Hevy service, denial of service by a trusted client with tool access, and deployments that expose the HTTP backend without the documented TLS/authentication/Host controls.

## Secret exposure response

If a Hevy key may have leaked, revoke or rotate it through Hevy, replace `HEVY_API_KEY`, and restart every affected container. If an HTTP bearer token may have leaked, replace it in authorized clients and recreate the HTTP container; the old token remains valid until the old process exits. Inspect client configuration, shell history, process supervision, reverse-proxy logs, and image build arguments for accidental values before restoring service.

Never post the exposed value in an issue, commit, diagnostic attachment, or reproduction.
