#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  printf '%s\n' "Usage: promote-ghcr-tag.sh IMAGE VERSION DIGEST" >&2
  exit 1
fi

image=$1
version=$2
intended_digest=$3
script_directory=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
"$script_directory/validate-sha256-digest.sh" "$intended_digest"

tag_state=$("$script_directory/ghcr-manifest.sh" "$image" "$version")
case $tag_state in
  absent)
    ;;
  "present $intended_digest")
    printf '%s\n' "The version tag already resolves to the verified digest; promotion is complete."
    exit 0
    ;;
  "present "*)
    printf '%s\n' "The version tag already resolves to a different digest." >&2
    exit 1
    ;;
  *)
    printf '%s\n' "The final version-tag state was invalid." >&2
    exit 1
    ;;
esac

exec docker buildx imagetools create \
  --tag "$image:$version" \
  "$image@$intended_digest"
