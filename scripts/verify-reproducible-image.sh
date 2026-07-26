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
  python_command=${HEVY_PYTHON_PATH:-python3}
  index_digest_file=$extraction/index-digest
  if ! "$python_command" - "$extraction/index.json" >"$index_digest_file" 2>/dev/null <<'PYTHON'
import json
import os
import re
import sys

MAX_DOCUMENT_BYTES = 4194304
MAX_DESCRIPTOR_SIZE = "9223372036854775807"

class StrictInteger(str):
  pass

def reject_non_integer(value):
  raise ValueError(f"non-integer JSON number: {value}")

def unique_object(pairs):
  result = {}
  for key, value in pairs:
    if key in result:
      raise ValueError(f"duplicate JSON key: {key}")
    result[key] = value
  return result

def is_positive_int64_token(value):
  return (
      isinstance(value, StrictInteger)
      and re.fullmatch(r"[1-9][0-9]*", value) is not None
      and (len(value) < len(MAX_DESCRIPTOR_SIZE)
           or (len(value) == len(MAX_DESCRIPTOR_SIZE) and value <= MAX_DESCRIPTOR_SIZE)))

if os.stat(sys.argv[1]).st_size > MAX_DOCUMENT_BYTES:
  raise ValueError("OCI archive index exceeds byte limit")
with open(sys.argv[1], "rb") as source:
  raw_document = source.read(MAX_DOCUMENT_BYTES + 1)
if len(raw_document) > MAX_DOCUMENT_BYTES:
  raise ValueError("OCI archive index exceeds byte limit")
document = json.loads(
      raw_document.decode("utf-8"),
      parse_int=StrictInteger,
      parse_float=reject_non_integer,
      parse_constant=reject_non_integer,
      object_pairs_hook=unique_object)

schema_version = document.get("schemaVersion") if isinstance(document, dict) else None
if not isinstance(schema_version, StrictInteger) or schema_version != "2":
  raise ValueError("invalid OCI layout schema")
manifests = document.get("manifests")
if not isinstance(manifests, list) or len(manifests) != 1:
  raise ValueError("invalid OCI layout descriptor count")
descriptor = manifests[0]
if not isinstance(descriptor, dict):
  raise ValueError("invalid OCI layout descriptor")
if descriptor.get("mediaType") != "application/vnd.oci.image.index.v1+json":
  raise ValueError("invalid OCI layout media type")
size = descriptor.get("size")
if not is_positive_int64_token(size):
  raise ValueError("invalid OCI layout descriptor size")
digest = descriptor.get("digest")
if not isinstance(digest, str) or re.fullmatch(r"sha256:[0-9a-f]{64}", digest) is None:
  raise ValueError("invalid OCI layout digest")
print(digest)
PYTHON
  then
    printf '%s\n' "The OCI archive index was not one complete strict JSON document of at most 4194304 bytes." >&2
    exit 1
  fi
  {
    IFS= read -r index_digest
    if IFS= read -r _; then
      printf '%s\n' "The OCI archive index produced more than one digest." >&2
      exit 1
    fi
  } < "$index_digest_file"
  "$script_directory/validate-sha256-digest.sh" "$index_digest"
  index_blob=$extraction/blobs/sha256/${index_digest#sha256:}
  validated_index=$({ "$script_directory/validate-oci-index.sh" "$index_blob" "$index_digest"; })
  amd64_digest=$(awk -F= '$1 == "amd64_digest" { print $2 }' <<<"$validated_index")
  arm64_digest=$(awk -F= '$1 == "arm64_digest" { print $2 }' <<<"$validated_index")

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

"$script_directory/validate-sha256-digest.sh" \
  "$first_index_digest" "$first_amd64_digest" "$first_arm64_digest"
if [[ -n ${GITHUB_OUTPUT:-} ]]; then
  printf 'source_date_epoch=%s\nindex_digest=%s\namd64_digest=%s\narm64_digest=%s\n' \
    "$source_date_epoch" \
    "$first_index_digest" \
    "$first_amd64_digest" \
    "$first_arm64_digest" >> "$GITHUB_OUTPUT"
fi
