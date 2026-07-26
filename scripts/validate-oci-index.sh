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

python_command=${HEVY_PYTHON_PATH:-python3}
if ! validated_index=$(
  "$python_command" - "$index_file" "$expected_index_digest" 2>/dev/null <<'PYTHON'
import hashlib
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
  raise ValueError("OCI index exceeds byte limit")
with open(sys.argv[1], "rb") as source:
  raw_document = source.read(MAX_DOCUMENT_BYTES + 1)
if len(raw_document) > MAX_DOCUMENT_BYTES:
  raise ValueError("OCI index exceeds byte limit")
if "sha256:" + hashlib.sha256(raw_document).hexdigest() != sys.argv[2]:
  raise ValueError("OCI index digest mismatch")
document = json.loads(
      raw_document.decode("utf-8"),
      parse_int=StrictInteger,
      parse_float=reject_non_integer,
      parse_constant=reject_non_integer,
      object_pairs_hook=unique_object)

schema_version = document.get("schemaVersion") if isinstance(document, dict) else None
if not isinstance(schema_version, StrictInteger) or schema_version != "2":
  raise ValueError("invalid OCI schema")
manifests = document.get("manifests")
if not isinstance(manifests, list) or len(manifests) != 2:
  raise ValueError("invalid OCI descriptor count")

platform_digests = {}
for descriptor in manifests:
  if not isinstance(descriptor, dict):
    raise ValueError("invalid OCI descriptor")
  if descriptor.get("mediaType") != "application/vnd.oci.image.manifest.v1+json":
    raise ValueError("invalid OCI manifest media type")
  size = descriptor.get("size")
  if not is_positive_int64_token(size):
    raise ValueError("invalid OCI descriptor size")
  digest = descriptor.get("digest")
  if not isinstance(digest, str) or re.fullmatch(r"sha256:[0-9a-f]{64}", digest) is None:
    raise ValueError("invalid OCI descriptor digest")
  platform = descriptor.get("platform")
  if not isinstance(platform, dict):
    raise ValueError("invalid OCI platform")
  identity = (platform.get("os"), platform.get("architecture"))
  if identity in platform_digests:
    raise ValueError("duplicate OCI platform")
  platform_digests[identity] = digest

required = {("linux", "amd64"), ("linux", "arm64")}
if set(platform_digests) != required or len(set(platform_digests.values())) != 2:
  raise ValueError("invalid OCI platform set")
print(platform_digests[("linux", "amd64")], platform_digests[("linux", "arm64")], sep="\t")
PYTHON
); then
  printf '%s\n' "The OCI index must be at most 4194304 bytes and exactly one complete JSON root with exactly two strict linux/amd64 and linux/arm64 descriptors." >&2
  exit 1
fi

IFS=$'\t' read -r amd64_digest arm64_digest unexpected <<<"$validated_index"
if [[ -z $amd64_digest || -z $arm64_digest || -n $unexpected ]]; then
  printf '%s\n' "The OCI index did not yield exactly one digest for each required platform." >&2
  exit 1
fi
"$script_directory/validate-sha256-digest.sh" "$amd64_digest" "$arm64_digest"
printf 'amd64_digest=%s\n' "$amd64_digest"
printf 'arm64_digest=%s\n' "$arm64_digest"
