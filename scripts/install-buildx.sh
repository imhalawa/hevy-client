#!/usr/bin/env sh
set -eu

version=0.35.0
archive=buildx-v0.35.0.linux-amd64
checksum=d41ece72044243b4f58b343441ae37446d9c29a7d6b5e11c61847bbcf8f7dfda
commit=a319e5b15052cf6557ceb666eb8ff6e32380b782
download_url="https://github.com/docker/buildx/releases/download/v$version/$archive"
expected_version="github.com/docker/buildx v$version $commit"

if [ -z "${RUNNER_TEMP:-}" ] || [ -z "${GITHUB_ENV:-}" ] || [ -z "${GITHUB_OUTPUT:-}" ]; then
  printf '%s\n' "RUNNER_TEMP, GITHUB_ENV, and GITHUB_OUTPUT are required." >&2
  exit 1
fi
curl_command=${HEVY_CURL_PATH:-curl}
sha256_command=${HEVY_SHA256SUM_PATH:-sha256sum}
temporary_directory=$(mktemp -d "$RUNNER_TEMP/hevy-buildx.XXXXXX")
trap 'rm -rf "$temporary_directory"' EXIT HUP INT TERM

"$curl_command" --silent --show-error --fail --location \
  --output "$temporary_directory/$archive" \
  "$download_url"
printf '%s  %s\n' "$checksum" "$temporary_directory/$archive" |
  "$sha256_command" --check --status
chmod 0755 "$temporary_directory/$archive"
actual_version=$("$temporary_directory/$archive" version)
if [ "$actual_version" != "$expected_version" ]; then
  printf '%s\n' "The downloaded Docker Buildx binary did not report the audited version and commit." >&2
  exit 1
fi

docker_config="$RUNNER_TEMP/hevy-buildx-bin"
plugin_directory="$docker_config/cli-plugins"
install -d -m 0755 "$plugin_directory"
install -m 0755 "$temporary_directory/$archive" "$plugin_directory/docker-buildx"
printf 'DOCKER_CONFIG=%s\n' "$docker_config" >> "$GITHUB_ENV"
printf 'buildx_path=%s\n' "$plugin_directory/docker-buildx" >> "$GITHUB_OUTPUT"
printf 'docker_config=%s\n' "$docker_config" >> "$GITHUB_OUTPUT"
