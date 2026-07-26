#!/bin/sh
set -eu

fail()
{
  printf '%s\n' "$1" >&2
  exit 1
}

require_value()
{
  variable_name="$1"
  eval "variable_value=\${$variable_name-}"
  [ -n "$variable_value" ] || fail "$variable_name is required."
}

for required_variable in \
  GITHUB_REF_TYPE \
  GITHUB_REF_NAME \
  GITHUB_SHA \
  GITHUB_REPOSITORY \
  GITHUB_SERVER_URL \
  GITHUB_OUTPUT \
  HEVY_CANONICAL_REPOSITORY \
  HEVY_PRIVATE_ADVISORY_VERIFIED
do
  require_value "$required_variable"
done

[ "$GITHUB_REF_TYPE" = "tag" ] || fail "Release validation requires a tag ref."
[ "$GITHUB_SERVER_URL" = "https://github.com" ] || fail "Public releases require the canonical https://github.com server."
[ "$HEVY_PRIVATE_ADVISORY_VERIFIED" = "true" ] || fail "Release blocked until private vulnerability reporting is enabled and verified."
[ "$HEVY_CANONICAL_REPOSITORY" = "$GITHUB_REPOSITORY" ] || fail "Configured canonical repository does not match the workflow repository."

if ! printf '%s\n' "$GITHUB_REPOSITORY" | grep -Eq '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$'; then
  fail "Repository identity is not a safe GitHub owner/name pair."
fi

if ! printf '%s\n' "$GITHUB_REF_NAME" | grep -Eq '^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'; then
  fail "Release tag must be exactly vX.Y.Z without prerelease metadata or leading zeroes."
fi

version=${GITHUB_REF_NAME#v}
old_ifs=$IFS
IFS=.
set -- $version
IFS=$old_ifs
for component in "$@"
do
  [ "$component" -le 65534 ] || fail "Release version components must fit .NET assembly version fields."
done

if ! printf '%s\n' "$GITHUB_SHA" | grep -Eq '^[0-9a-f]{40}$'; then
  fail "GITHUB_SHA must be the full lowercase source commit."
fi

tag_type=$(git cat-file -t "refs/tags/$GITHUB_REF_NAME" 2>/dev/null) || fail "Release tag is missing from the checked-out repository."
case "$tag_type" in
  commit|tag) ;;
  *) fail "Release ref is neither a lightweight nor annotated Git tag." ;;
esac

tag_commit=$(git rev-parse --verify "refs/tags/${GITHUB_REF_NAME}^{commit}") || fail "Release tag does not resolve to a commit."
head_commit=$(git rev-parse --verify HEAD) || fail "Checked-out source has no commit identity."
[ "$tag_commit" = "$head_commit" ] || fail "Checked-out source does not match the release tag."
[ "$tag_commit" = "$GITHUB_SHA" ] || fail "Workflow source SHA does not match the release tag commit."

case "$GITHUB_OUTPUT" in
  /*) ;;
  *) fail "GITHUB_OUTPUT must be an absolute runner-managed path." ;;
esac

image_repository=$(printf '%s' "$GITHUB_REPOSITORY" | tr '[:upper:]' '[:lower:]')
source_url="https://github.com/$GITHUB_REPOSITORY"
{
  printf 'version=%s\n' "$version"
  printf 'revision=%s\n' "$tag_commit"
  printf 'source=%s\n' "$source_url"
  printf 'image=ghcr.io/%s\n' "$image_repository"
} >> "$GITHUB_OUTPUT"
