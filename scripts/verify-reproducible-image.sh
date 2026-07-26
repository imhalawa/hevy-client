#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 0 ]]; then
  printf '%s\n' "Usage: verify-reproducible-image.sh" >&2
  exit 1
fi

script_directory=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repository_root=$(CDPATH= cd -- "$script_directory/.." && pwd)
version=${VERSION:-0.0.0}
revision=${REVISION:-$(git -C "$repository_root" rev-parse HEAD)}
source_url=${SOURCE_URL:-}
if [[ -z $source_url ]]; then
  if [[ -n ${GITHUB_SERVER_URL:-} && -n ${GITHUB_REPOSITORY:-} ]]; then
    source_url=$GITHUB_SERVER_URL/$GITHUB_REPOSITORY
  else
    source_url=https://github.com/example/hevy-client
  fi
fi
source_date_epoch=${SOURCE_DATE_EPOCH:-$(git -C "$repository_root" show -s --format=%ct "$revision")}

if [[ ! $version =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]] ||
   [[ ! $revision =~ ^[0-9a-f]{40}$ ]] ||
   [[ ! $source_url =~ ^https://[^[:space:]]+$ ]] ||
   [[ ! $source_date_epoch =~ ^[1-9][0-9]*$ ]]; then
  printf '%s\n' "The reproducibility build identity was invalid." >&2
  exit 1
fi

temporary_directory=$(mktemp -d)
trap 'rm -rf "$temporary_directory"' EXIT HUP INT TERM
if [[ -n ${HEVY_BUILDX_PATH:-} ]]; then
  if [[ $HEVY_BUILDX_PATH != /* || ! -x $HEVY_BUILDX_PATH ]]; then
    printf '%s\n' "HEVY_BUILDX_PATH must name an absolute executable." >&2
    exit 1
  fi
  buildx_command=("$HEVY_BUILDX_PATH")
else
  buildx_command=(docker buildx)
fi
first_index_digest=
first_amd64_digest=
first_arm64_digest=

for run_number in 1 2; do
  archive=$temporary_directory/repro-$run_number.tar
  extraction=$temporary_directory/extract-$run_number
  mkdir "$extraction"
  SOURCE_DATE_EPOCH=$source_date_epoch "${buildx_command[@]}" build \
    --platform linux/amd64,linux/arm64 \
    --pull \
    --no-cache \
    --build-arg "VERSION=$version" \
    --build-arg "REVISION=$revision" \
    --build-arg "SOURCE_URL=$source_url" \
    --provenance=false \
    --sbom=false \
    --output "type=oci,dest=$archive,rewrite-timestamp=true,compatibility-version=30,oci-mediatypes=true" \
    "$repository_root"

  tar --extract --file "$archive" --directory "$extraction"
  index_digest=$(jq -er 'select(.manifests | length == 1) | .manifests[0].digest' "$extraction/index.json")
  "$script_directory/validate-sha256-digest.sh" "$index_digest"
  index_blob=$extraction/blobs/sha256/${index_digest#sha256:}
  jq -e '
    (.manifests | length) == 2 and
    ([.manifests[] | (.platform.os + "/" + .platform.architecture)] | sort == ["linux/amd64", "linux/arm64"])
  ' "$index_blob" >/dev/null
  amd64_digest=$(jq -er '.manifests[] | select(.platform.os == "linux" and .platform.architecture == "amd64") | .digest' "$index_blob")
  arm64_digest=$(jq -er '.manifests[] | select(.platform.os == "linux" and .platform.architecture == "arm64") | .digest' "$index_blob")
  "$script_directory/validate-sha256-digest.sh" "$amd64_digest" "$arm64_digest"

  printf 'repro_run_%s=%s\n' "$run_number" "$index_digest"
  printf 'repro_run_%s_amd64=%s\n' "$run_number" "$amd64_digest"
  printf 'repro_run_%s_arm64=%s\n' "$run_number" "$arm64_digest"
  if [[ $run_number -eq 1 ]]; then
    first_index_digest=$index_digest
    first_amd64_digest=$amd64_digest
    first_arm64_digest=$arm64_digest
  elif [[ $index_digest != "$first_index_digest" ]] ||
       [[ $amd64_digest != "$first_amd64_digest" ]] ||
       [[ $arm64_digest != "$first_arm64_digest" ]]; then
    printf '%s\n' "Repeated multi-architecture builds were not reproducible." >&2
    exit 1
  fi
done
