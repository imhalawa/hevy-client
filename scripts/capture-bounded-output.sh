#!/usr/bin/env bash
set -euo pipefail

readonly maximum_bytes=4194304
readonly read_limit=$((maximum_bytes + 1))

if [[ $# -lt 2 ]]; then
  printf '%s\n' "Usage: capture-bounded-output.sh OUTPUT_FILE COMMAND [ARGUMENT ...]" >&2
  exit 1
fi

output_file=$1
shift
output_directory=$(dirname -- "$output_file")
output_name=$(basename -- "$output_file")
if [[ ! -d $output_directory || -d $output_file || -z $output_name || $output_name == . || $output_name == .. ]]; then
  printf '%s\n' "The bounded output destination was invalid." >&2
  exit 1
fi

temporary_file=$(mktemp "$output_directory/.${output_name}.tmp.XXXXXX")
cleanup() {
  if [[ -n ${temporary_file:-} ]]; then
    rm -f -- "$temporary_file"
  fi
}
trap cleanup EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM

set +e
"$@" | head -c "$read_limit" > "$temporary_file"
pipeline_status=("${PIPESTATUS[@]}")
set -e
if ((pipeline_status[1] != 0)); then
  exit "${pipeline_status[1]}"
fi

captured_size=$(wc -c < "$temporary_file")
if ((captured_size > maximum_bytes)); then
  printf '%s\n' "Command output exceeded the 4194304-byte limit." >&2
  exit 1
fi
if ((pipeline_status[0] != 0)); then
  exit "${pipeline_status[0]}"
fi

mv -- "$temporary_file" "$output_file"
temporary_file=
