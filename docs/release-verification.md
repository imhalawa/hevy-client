# Release verification

## v0.1.0

Pull the immutable image:

```sh
docker pull ghcr.io/imhalawa/hevy-mcp@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841
```

Verify its keyless signature:

```sh
cosign verify ghcr.io/imhalawa/hevy-mcp@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841 \
  --certificate-identity https://github.com/imhalawa/hevy-mcp/.github/workflows/release.yml@refs/tags/v0.1.0 \
  --certificate-github-workflow-sha 9ec3223c6bfe72d57435a50c8d0f19eb92d0624e \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

Verify the GitHub provenance and SBOM attestations:

```sh
gh attestation verify oci://ghcr.io/imhalawa/hevy-mcp@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841 \
  --repo imhalawa/hevy-mcp \
  --signer-workflow imhalawa/hevy-mcp/.github/workflows/release.yml \
  --source-digest 9ec3223c6bfe72d57435a50c8d0f19eb92d0624e \
  --source-ref refs/tags/v0.1.0 \
  --bundle-from-oci
```

The digest identifies immutable image content. The `v0.1.0` tag is a reviewed
convenience reference, not an immutable identifier. Maintainers follow the
[public distribution release checklist](release-checklist.md) before publishing.
