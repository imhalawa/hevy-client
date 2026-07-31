# Public distribution release checklist

This is a release-blocking handoff to Task 10. An unchecked blocking item means **BLOCK RELEASE**: do not push a public image, create a public release, or advertise a supported vulnerability-reporting route.

## Canonical repository and security intake

- [x] The canonical `imhalawa/hevy-client` GitHub remote is configured and owned by the intended maintainer.
- [x] GitHub **Private vulnerability reporting** is enabled and confirmed through the repository API.
- [x] The `../../security/advisories/new` route resolves on the canonical repository. A separate authenticated non-owner account was not available for this release.
- [x] `SECURITY.md` renders the same route and contains no invented email address, owner, or public-issue fallback for suspected vulnerabilities.
- [x] Public issues are enabled only as the documented path for non-sensitive bugs.
- [x] A protected GitHub Actions environment named `release` has a required reviewer, and `.github/workflows/release.yml` is the repository's only workflow with `packages: write`.
- [x] `HEVY_CANONICAL_REPOSITORY` is `imhalawa/hevy-client`, and `HEVY_PRIVATE_ADVISORY_VERIFIED` was set to `true` only after the checks above passed.
- [x] Immutable GitHub releases are enabled for the repository.

## Release identity

- [x] Release `v0.1.0` is valid semantic versioning and resolves to commit `9ec3223c6bfe72d57435a50c8d0f19eb92d0624e`.
- [x] The container build received `VERSION=0.1.0`, the full source `REVISION`, and `SOURCE_URL=https://github.com/imhalawa/hevy-client` as non-secret build arguments.
- [x] Image inspection proved those exact values appear in both platform OCI labels; no development version remains.
- [x] The README records the canonical package, `v0.1.0` verification identity, and immutable digest `sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841`.
- [x] From an authenticated maintainer shell, `GITHUB_ACTOR=imhalawa GHCR_TOKEN="$(gh auth token)" ./scripts/ghcr-manifest.sh ghcr.io/imhalawa/hevy-client 0.0.0` returned `absent`. The helper performed only HEAD/token GET requests; the token was neither passed as an argument nor captured through shell tracing.
- [x] The protected `release` environment validated a complete disposable release and same-digest rerun at `https://github.com/imhalawa/hevy-client-release-probe-20260731/actions/runs/30652756484`. Conflict run `https://github.com/imhalawa/hevy-client-release-probe-20260731/actions/runs/30654159925` passed every verification and then refused `sha256:e2f12d4fe5adac057165bc66de4ab767e3763d37cd85a6ba21472dc88c32182e` because the tag already resolved to `sha256:f2b91f8e9c2a4e6f6587d09740c969a33d1f8bd905f56cfbcb4e3af1482f6086`. Cleanup run `30654874432` removed the package, then the disposable repository was deleted. The recorded URLs remain audit identifiers for the deleted repository.

## Required Task 10 verification

- [x] Locked restore, format verification, Release build, 590 non-live tests, OpenAPI checks, pinned Buildx proof, two matching no-cache multi-architecture exports, and seven container smokes passed in tag CI run `https://github.com/imhalawa/hevy-client/actions/runs/30654942156` and release run `https://github.com/imhalawa/hevy-client/actions/runs/30654942141`.
- [x] Executable contracts prove CI fails rather than skips for every unavailable or misconfigured Docker state; the tag CI run passed those contracts.
- [x] Before signing, the staged index contained only linux/amd64 `sha256:77c781c75a6d0579efb1240199194ccbfe7d66a4047499c8ba0f4d31c5a8407a` and linux/arm64 `sha256:9cefed73d213859d5c9c68a1fc571314f12498c1aee6fe0486d01e1a1b862aee`. The index matched both reproducibility exports at `sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841`; SPDX 2.3, provenance, keyless signing, and attestations passed before promotion.
- [x] Release concurrency is repository-wide, the protected environment admits one reviewed writer, and supported consumer references use the immutable digest because GHCR has no atomic create-only tag operation.
- [x] Probe attempt 2 proved a same-digest rerun succeeds without moving the tag; the conflict run proved a different digest blocks promotion after verification.
- [x] A maintainer downloaded both 90-day SPDX artifacts, attached them to the draft, repeated the pinned Cosign v3.0.6 identity/commit check and `gh attestation verify`, then published `https://github.com/imhalawa/hevy-client/releases/tag/v0.1.0`.
- [x] Repository secret, telemetry, non-Hevy-origin, placeholder, tracked-artifact, C# comment, and assertion-policy scans have zero unexplained findings. Tests use FluentAssertions 7.2.2; the only retained C# comment is essential XML documentation.
