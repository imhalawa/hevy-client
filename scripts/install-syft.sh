#!/usr/bin/env sh
set -eu

version=1.49.0
archive=syft_1.49.0_linux_amd64.tar.gz
checksum=7aa2f03ee92739cf643279ba3990548b9925d4e22cae13f46831ee62821147fe
download_url="https://github.com/anchore/syft/releases/download/v$version/$archive"

if [ -z "${RUNNER_TEMP:-}" ] || [ -z "${GITHUB_PATH:-}" ]; then
  printf '%s\n' "RUNNER_TEMP and GITHUB_PATH are required." >&2
  exit 1
fi

temporary_directory=$(mktemp -d "$RUNNER_TEMP/hevy-syft.XXXXXX")
trap 'rm -rf "$temporary_directory"' EXIT HUP INT TERM

curl --silent --show-error --fail --location \
  --output "$temporary_directory/$archive" \
  "$download_url"
printf '%s  %s\n' "$checksum" "$temporary_directory/$archive" |
  sha256sum --check --status
tar --extract --gzip \
  --file "$temporary_directory/$archive" \
  --directory "$temporary_directory" \
  syft
chmod 0755 "$temporary_directory/syft"
install_directory=$(mktemp -d "$RUNNER_TEMP/hevy-syft-bin.XXXXXX")
mv "$temporary_directory/syft" "$install_directory/syft"
printf '%s\n' "$install_directory" >> "$GITHUB_PATH"
