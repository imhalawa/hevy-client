# Public distribution release checklist

This is a release-blocking handoff to Task 10. An unchecked blocking item means **BLOCK RELEASE**: do not push a public image, create a public release, or advertise a supported vulnerability-reporting route.

## Canonical repository and security intake

- [ ] The canonical GitHub repository remote is configured and owned by the intended maintainer or organization.
- [ ] GitHub **Private vulnerability reporting** is enabled under repository security settings.
- [ ] While authenticated as a non-owner test user where practical, the `../../security/advisories/new` link in `SECURITY.md` opens the canonical repository's private advisory form.
- [ ] `SECURITY.md` renders the same route and contains no invented email address, owner, or public-issue fallback for suspected vulnerabilities.
- [ ] Public issues are enabled only as the documented path for non-sensitive bugs.
- [ ] A protected GitHub Actions environment named `release` exists with required reviewer protection, and repository policy makes `.github/workflows/release.yml` the only GHCR package writer.
- [ ] `HEVY_CANONICAL_REPOSITORY` exactly matches the canonical `OWNER/REPOSITORY`, and `HEVY_PRIVATE_ADVISORY_VERIFIED` is set to `true` only after the checks above pass.
- [ ] Immutable GitHub releases are enabled where available; otherwise maintainers follow the draft-assets-first publication sequence and never move a published release tag.

## Release identity

- [ ] The release version is valid semantic versioning and matches the immutable Git tag.
- [ ] The container build receives `VERSION`, the full 40-character source `REVISION`, and the canonical HTTPS `SOURCE_URL` as non-secret build arguments.
- [ ] Image inspection proves those exact values appear in OCI labels; no development `local` or `0.0.0-dev` value remains.
- [ ] The README's final registry name, version tag, and immutable digest examples match the canonical repository.
- [ ] From an authenticated maintainer shell, run `GITHUB_ACTOR=OWNER GHCR_TOKEN="$(gh auth token)" ./scripts/ghcr-manifest.sh ghcr.io/OWNER/REPOSITORY 0.0.0`; it performs only HEAD/token GET requests and must return `absent`. Do not place the token in an argument or capture shell tracing.
- [ ] Before the first supported release, use the protected `release` environment and a disposable repository/package under the same owner to validate the complete GHCR write, attestation, signature, same-digest idempotency, and conflicting-digest refusal paths. Record the run URL and remove the disposable package afterward. Local acceptance cannot authorize this external write.

## Required Task 10 verification

- [ ] Locked restore, format verification, Release build, all non-live tests, OpenAPI checks, the checksum-pinned Buildx version/source-commit proof, two no-cache multi-architecture exports with exactly matching index/platform digests, and real container smokes pass in CI.
- [ ] CI fails rather than skips for every unavailable or misconfigured Docker state.
- [ ] Before SBOM/provenance/signing, the raw staged index has exactly two total linux/amd64 and linux/arm64 descriptors and its top and platform digests all equal the reproducibility-gate outputs. SPDX 2.3 SBOM, provenance attestations, the exact staged digest, and the keyless signature are then verified before version-tag promotion.
- [ ] Release workflow concurrency is repository-wide, the protected environment admits one writer, and maintainers understand that GHCR has no registry-enforced atomic create-only or immutable-tag operation. Supported consumer references use the digest.
- [ ] If final promotion reports a transport or post-job failure, authenticate the tag lookup and compare it with the failed run's verified build digest. The same digest is success and an absent tag permits a serialized retry because attestations are external to the reproducible staged index. Only a genuinely different or unverifiable digest enters manual recovery and blocks release pending source, Cosign, and GitHub-attestation investigation.
- [ ] After the workflow promotes the verified GHCR digest, a maintainer downloads its 90-day SPDX workflow artifacts, attaches them to the draft GitHub Release, repeats both the documented Cosign identity check and `gh attestation verify`, and only then publishes that GitHub Release.
- [ ] Repository secret, telemetry, non-Hevy-origin, placeholder, and tracked-artifact scans have zero unexplained findings.
