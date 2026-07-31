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

- [ ] The release version is valid semantic versioning and matches the immutable Git tag.
- [ ] The container build receives `VERSION`, the full 40-character source `REVISION`, and the canonical HTTPS `SOURCE_URL` as non-secret build arguments.
- [ ] Image inspection proves those exact values appear in OCI labels; no development `local` or `0.0.0-dev` value remains.
- [ ] The README's final registry name, version tag, and immutable digest examples match the canonical repository.
- [x] From an authenticated maintainer shell, `GITHUB_ACTOR=imhalawa GHCR_TOKEN="$(gh auth token)" ./scripts/ghcr-manifest.sh ghcr.io/imhalawa/hevy-client 0.0.0` returned `absent`. The helper performed only HEAD/token GET requests; the token was neither passed as an argument nor captured through shell tracing.
- [ ] Before the first supported release, use the protected `release` environment and a disposable repository/package under the same owner to validate the complete GHCR write, attestation, signature, same-digest idempotency, and conflicting-digest refusal paths. Record the run URL and remove the disposable package afterward. Local acceptance cannot authorize this external write.

## Required Task 10 verification

- [ ] Locked restore, format verification, Release build, all non-live tests, OpenAPI checks, the checksum-pinned Buildx version/source-commit proof, two no-cache multi-architecture exports with exactly matching index/platform digests, and real container smokes pass in CI.
- [ ] CI fails rather than skips for every unavailable or misconfigured Docker state.
- [ ] Before SBOM/provenance/signing, the raw staged index has exactly two total linux/amd64 and linux/arm64 descriptors and its top and platform digests all equal the reproducibility-gate outputs. SPDX 2.3 SBOM, provenance attestations, the exact staged digest, and the keyless signature are then verified before version-tag promotion.
- [ ] Release workflow concurrency is repository-wide, the protected environment admits one writer, and maintainers understand that GHCR has no registry-enforced atomic create-only or immutable-tag operation. Supported consumer references use the digest.
- [ ] If final promotion reports a transport or post-job failure, authenticate the tag lookup and compare it with the failed run's verified build digest. The same digest is success and an absent tag permits a serialized retry because attestations are external to the reproducible staged index. Only a genuinely different or unverifiable digest enters manual recovery and blocks release pending source, Cosign, and GitHub-attestation investigation.
- [ ] After the workflow promotes the verified GHCR digest, a maintainer downloads its 90-day SPDX workflow artifacts, attaches them to the draft GitHub Release, repeats both the documented Cosign identity check and `gh attestation verify`, and only then publishes that GitHub Release.
- [ ] Repository secret, telemetry, non-Hevy-origin, placeholder, tracked-artifact, C# comment, and assertion-policy scans have zero unexplained findings.
