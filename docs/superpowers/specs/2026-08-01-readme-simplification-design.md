# README simplification design

**Date:** 2026-08-01

## Goal

Make the repository immediately understandable and runnable for a developer
who discovers it as an AI/MCP project. The README should explain what Hevy is,
what this server enables, and how to start the released image without asking
the reader to understand the release pipeline first.

## Reader path

The README will use a progressive first-use path:

1. **What it is** — a short definition of Hevy and of this local MCP server.
2. **What it enables** — typed Hevy API access, search, bounded summaries, and
   deterministic training analysis for an MCP-capable AI client.
3. **Quick start** — obtain a Hevy API key, pull the published image, set the
   caller's own `HEVY_API_KEY`, and run the hardened stdio container. The
   expected behavior is stated: it waits for MCP JSON-RPC on standard input.
4. **Connect an AI client** — one concise Codex example and an explicitly
   generic Docker argument list for other stdio clients.
5. **Capabilities and safety** — compact summaries of writes, read-only mode,
   dry runs, single-tenancy, credential handling, and the lack of telemetry.
6. **Advanced operation** — optional authenticated HTTP hosting and a short
   configuration reference.
7. **Project links** — contribution, security reporting, release verification,
   and license.

The quick start uses the public, immutable image digest. It explains that the
image contains no API key and that `-e HEVY_API_KEY` passes the caller's value
only when the container starts. Windows receives a short PowerShell path beside
the portable shell command.

## Content boundaries

The README remains the source of the commands and guardrails needed for normal
use. It does not repeat the full internal release workflow, multi-architecture
reproducibility process, GitHub Action pinning rationale, or public-distribution
checklist. Those remain in the existing release checklist and release artifacts,
with a short verification link from the README.

Desktop-client and secret-store instructions are reduced to the one fact that
changes setup: graphical clients must obtain the key before they launch the
container. The existing platform launchers remain in the repository and are
linked rather than explained at tutorial length.

## Safety and correctness

All surviving commands preserve the current container controls: attached stdin,
a read-only filesystem, a no-exec tmpfs, and no published port for stdio. HTTP
guidance retains the separate bearer token, loopback binding, TLS reverse proxy,
and single-tenant boundary. No API key, bearer token, or real-looking secret is
added to the README.

Configuration names, image digest, public repository coordinates, supported
operations, and protocol behavior are copied only from the current repository
and release metadata. The rewrite changes no application, image, workflow, or
release behavior.

## Verification

Review the README for a clear first-run sequence, valid image reference,
complete container command, correct security boundaries, and working links to
the retained detail. Run the repository's documentation and delivery contract
tests, then inspect the rendered Markdown headings and command blocks.
