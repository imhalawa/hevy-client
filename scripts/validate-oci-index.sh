#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  printf '%s\n' "Usage: validate-oci-index.sh INDEX_JSON EXPECTED_INDEX_DIGEST" >&2
  exit 1
fi

script_directory=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
index_file=$1
expected_index_digest=$2
if [[ ! -f $index_file ]]; then
  printf '%s\n' "The OCI index JSON file does not exist." >&2
  exit 1
fi
"$script_directory/validate-sha256-digest.sh" "$expected_index_digest"
actual_index_digest=sha256:$(sha256sum "$index_file" | awk '{print $1}')
if [[ $actual_index_digest != "$expected_index_digest" ]]; then
  printf '%s\n' "The raw OCI index does not match its expected digest." >&2
  exit 1
fi

if ! jq -e '
  .schemaVersion == 2 and
  (.manifests | type == "array" and length == 2) and
  (all(.manifests[];
    .mediaType == "application/vnd.oci.image.manifest.v1+json" and
    (.size | type == "number" and . > 0) and
    (.digest | type == "string" and test("^sha256:[0-9a-f]{64}$")) and
    (.platform | type == "object") and
    (.platform.os | type == "string") and
    (.platform.architecture | type == "string"))) and
  ([.manifests[] | [.platform.os, .platform.architecture]] | sort == [["linux", "amd64"], ["linux", "arm64"]]) and
  ([.manifests[].digest] | unique | length == 2)
' "$index_file" >/dev/null; then
  printf '%s\n' "The OCI index must contain exactly two total descriptors: linux/amd64 and linux/arm64." >&2
  exit 1
fi

amd64_digest=$(jq -er '.manifests[] | select(.platform.os == "linux" and .platform.architecture == "amd64") | .digest' "$index_file")
arm64_digest=$(jq -er '.manifests[] | select(.platform.os == "linux" and .platform.architecture == "arm64") | .digest' "$index_file")
printf 'amd64_digest=%s\n' "$amd64_digest"
printf 'arm64_digest=%s\n' "$arm64_digest"
