# hevy-mcp rename design

**Date:** 2026-08-01

## Decision

Rename the entire project from `hevy-client` to `hevy-mcp`. The new public
identity is `imhalawa/hevy-mcp` and its public image is
`ghcr.io/imhalawa/hevy-mcp`. The previous `hevy-client` repository, GHCR image,
and v0.1.0 release remain historical only.

## Rename boundary

The rename applies to every current project identity:

- GitHub repository name and local `origin` remote.
- GHCR package, Docker image title, image tags, container names, script names,
  release metadata, and workflow package coordinates.
- Solution name, project directories, `.csproj` names, assembly names, root
  namespaces, test project names, and all C# imports and qualified type names.
- Public type names that carry the old product identity, including
  `HevyClient`, `IHevyClient`, `HevyClientOptions`, and test fakes.
- Shell and PowerShell launcher filenames, documentation links, CI scripts,
  Docker smoke tests, release checks, and user-visible text.

The typed connection to the upstream Hevy API remains a client internally, but
its code identity changes consistently to `HevyMcpClient` under the
`Hevy.Mcp.Client` namespace. The server project remains `Hevy.Mcp` because it
already names the target product and protocol.

## Historical exceptions

Historical evidence must not be rewritten. It continues to name
`imhalawa/hevy-client`, `ghcr.io/imhalawa/hevy-client`, and v0.1.0 where those
values identify an already-published release, signed digest, GitHub workflow
identity, attestation, SBOM, or audit record. The release-verification guide
will label those values as legacy verification evidence and direct new users to
`hevy-mcp`.

## Migration sequence

1. Rename the GitHub repository through GitHub so GitHub provides the normal
   repository redirect. Update `origin` to the new SSH URL.
2. Rename all version-controlled local files and symbols atomically, then
   update solution references and namespaces.
3. Update Docker, CI, release, package, and documentation references to the
   new repository/package while preserving only the documented historical
   v0.1.0 references.
4. Build and run the full test suite. Container and release contract tests must
   assert the new current identity and still permit the explicitly scoped
   historical evidence.
5. Regenerate release artifacts only through the existing protected release
   workflow. Do not overwrite or delete the old public image; the first
   `hevy-mcp` release establishes the new package identity.

## Dependabot and CPM

Central Package Management remains required. `Directory.Packages.props` owns
all package versions, every project continues to use versionless
`PackageReference` entries, and Dependabot updates that central file plus the
locked dependency graphs. The current open Dependabot PRs will be rebased or
recreated against the renamed repository only after the rename lands.
