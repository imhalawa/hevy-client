#!/bin/sh
set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
snapshot="$repository_root/docs/api/hevy-openapi-2026-07-26.json"

jq -e '
  .info.version == "0.0.1" and
  (.paths | length) == 14 and
  ([.paths[] | to_entries[] | select(.key == "get" or .key == "post" or .key == "put")] | length) == 22 and
  ([.paths[] | to_entries[] | select(.key == "get")] | length) == 14 and
  ([.paths[] | to_entries[] | select(.key == "post" or .key == "put")] | length) == 8 and
  ([.paths | keys[] | select(startswith("/v1/") | not)] | length) == 0 and
  ((.servers // []) | all(.url == "https://api.hevyapp.com"))
' "$snapshot" >/dev/null
