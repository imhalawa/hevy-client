#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 5 ]]; then
  printf '%s\n' "Usage: verify-staged-index.sh INDEX_JSON ACTUAL_INDEX_DIGEST EXPECTED_INDEX_DIGEST EXPECTED_AMD64_DIGEST EXPECTED_ARM64_DIGEST" >&2
  exit 1
fi

script_directory=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
index_file=$1
actual_index_digest=$2
expected_index_digest=$3
expected_amd64_digest=$4
expected_arm64_digest=$5
"$script_directory/validate-sha256-digest.sh" \
  "$actual_index_digest" "$expected_index_digest" "$expected_amd64_digest" "$expected_arm64_digest"
if [[ $actual_index_digest != "$expected_index_digest" ]]; then
  printf '%s\n' "The staged index digest differs from the reproducibility gate." >&2
  exit 1
fi

validated=$({ "$script_directory/validate-oci-index.sh" "$index_file" "$actual_index_digest"; })
amd64_digest=$(awk -F= '$1 == "amd64_digest" { print $2 }' <<<"$validated")
arm64_digest=$(awk -F= '$1 == "arm64_digest" { print $2 }' <<<"$validated")
if [[ $amd64_digest != "$expected_amd64_digest" ]]; then
  printf '%s\n' "The staged amd64 digest differs from the reproducibility gate." >&2
  exit 1
fi
if [[ $arm64_digest != "$expected_arm64_digest" ]]; then
  printf '%s\n' "The staged arm64 digest differs from the reproducibility gate." >&2
  exit 1
fi
if [[ -z ${GITHUB_OUTPUT:-} ]]; then
  printf '%s\n' "GITHUB_OUTPUT is required." >&2
  exit 1
fi
printf 'amd64_digest=%s\n' "$amd64_digest" >> "$GITHUB_OUTPUT"
printf 'arm64_digest=%s\n' "$arm64_digest" >> "$GITHUB_OUTPUT"
