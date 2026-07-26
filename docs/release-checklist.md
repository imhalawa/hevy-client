# Public distribution release checklist

This is a release-blocking handoff to Task 10. An unchecked blocking item means **BLOCK RELEASE**: do not push a public image, create a public release, or advertise a supported vulnerability-reporting route.

## Canonical repository and security intake

- [ ] The canonical GitHub repository remote is configured and owned by the intended maintainer or organization.
- [ ] GitHub **Private vulnerability reporting** is enabled under repository security settings.
- [ ] While authenticated as a non-owner test user where practical, the `../../security/advisories/new` link in `SECURITY.md` opens the canonical repository's private advisory form.
- [ ] `SECURITY.md` renders the same route and contains no invented email address, owner, or public-issue fallback for suspected vulnerabilities.
- [ ] Public issues are enabled only as the documented path for non-sensitive bugs.
- [ ] A protected GitHub Actions environment named `release` exists, with required reviewer protection where the hosting plan supports it.
- [ ] `HEVY_CANONICAL_REPOSITORY` exactly matches the canonical `OWNER/REPOSITORY`, and `HEVY_PRIVATE_ADVISORY_VERIFIED` is set to `true` only after the checks above pass.
- [ ] Immutable GitHub releases are enabled where available; otherwise maintainers follow the draft-assets-first publication sequence and never move a published release tag.

## Release identity

- [ ] The release version is valid semantic versioning and matches the immutable Git tag.
- [ ] The container build receives `VERSION`, the full 40-character source `REVISION`, and the canonical HTTPS `SOURCE_URL` as non-secret build arguments.
- [ ] Image inspection proves those exact values appear in OCI labels; no development `local` or `0.0.0-dev` value remains.
- [ ] The README's final registry name and immutable version/digest examples match the canonical repository.

## Required Task 10 verification

- [ ] Locked restore, format verification, Release build, all non-live tests, OpenAPI checks, multi-architecture container build, and real container smokes pass in CI.
- [ ] CI fails rather than skips for every unavailable or misconfigured Docker state.
- [ ] SBOM, provenance attestation, immutable GHCR tags, and keyless signature are verified before release publication.
- [ ] After the workflow promotes the verified GHCR digest, a maintainer downloads its 90-day SPDX workflow artifacts, attaches them to the draft GitHub Release, repeats both the documented Cosign identity check and `gh attestation verify`, and only then publishes that GitHub Release.
- [ ] Repository secret, telemetry, non-Hevy-origin, placeholder, and tracked-artifact scans have zero unexplained findings.
