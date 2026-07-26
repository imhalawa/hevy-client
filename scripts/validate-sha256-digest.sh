#!/bin/sh
set -eu

if [ "$#" -eq 0 ]; then
  printf '%s\n' "At least one SHA-256 digest is required." >&2
  exit 1
fi

for digest do
  if ! printf '%s\n' "$digest" | grep -Eq '^sha256:[0-9a-f]{64}$'; then
    printf '%s\n' "A value was not an immutable SHA-256 digest." >&2
    exit 1
  fi
done
