#!/bin/sh
set -eu

version=1.7.12
archive=actionlint_1.7.12_linux_amd64.tar.gz
checksum=8aca8db96f1b94770f1b0d72b6dddcb1ebb8123cb3712530b08cc387b349a3d8
url="https://github.com/rhysd/actionlint/releases/download/v$version/$archive"

[ "$(uname -s)" = "Linux" ] || { printf '%s\n' "Pinned actionlint runner supports Linux only." >&2; exit 1; }
[ "$(uname -m)" = "x86_64" ] || { printf '%s\n' "Pinned actionlint runner supports x86_64 only." >&2; exit 1; }

temporary_directory=$(mktemp -d)
trap 'rm -rf "$temporary_directory"' EXIT HUP INT TERM
curl_command=${HEVY_CURL_PATH:-curl}
sha256_command=${HEVY_SHA256SUM_PATH:-sha256sum}
"$curl_command" --fail --location --proto '=https' --tlsv1.2 --output "$temporary_directory/$archive" "$url"
printf '%s  %s\n' "$checksum" "$temporary_directory/$archive" | "$sha256_command" --check --status
tar -xzf "$temporary_directory/$archive" -C "$temporary_directory" actionlint
chmod 0755 "$temporary_directory/actionlint"
"$temporary_directory/actionlint" -color
