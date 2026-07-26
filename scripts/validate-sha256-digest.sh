#!/bin/sh
set -eu

if [ "$#" -eq 0 ]; then
  printf '%s\n' "At least one SHA-256 digest is required." >&2
  exit 1
fi

for digest do
  case $digest in
    sha256:*) hexadecimal=${digest#sha256:} ;;
    *) hexadecimal= ;;
  esac
  if [ "${#hexadecimal}" -ne 64 ]; then
    printf '%s\n' "A value was not an immutable SHA-256 digest." >&2
    exit 1
  fi
  case $hexadecimal in
    *[!0-9a-f]*)
      printf '%s\n' "A value was not an immutable SHA-256 digest." >&2
      exit 1
      ;;
  esac
done
