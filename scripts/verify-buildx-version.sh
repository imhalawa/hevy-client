#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 0 ]]; then
  printf '%s\n' "Usage: verify-buildx-version.sh" >&2
  exit 1
fi

expected='github.com/docker/buildx v0.35.0 a319e5b15052cf6557ceb666eb8ff6e32380b782'
if [[ -n ${HEVY_BUILDX_PATH:-} ]]; then
  if [[ $HEVY_BUILDX_PATH != /* || ! -x $HEVY_BUILDX_PATH ]]; then
    printf '%s\n' "HEVY_BUILDX_PATH must name an absolute executable." >&2
    exit 1
  fi
  actual=$("$HEVY_BUILDX_PATH" version)
else
  actual=$(docker buildx version)
fi
if [[ $actual != "$expected" ]]; then
  printf '%s\n' "The active Docker Buildx version or source commit was not the audited pin." >&2
  exit 1
fi
