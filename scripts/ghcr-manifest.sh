#!/usr/bin/env bash
set -euo pipefail

fail()
{
  printf '%s\n' "$1" >&2
  exit 1
}

if [[ $# -ne 2 ]]; then
  fail "Usage: ghcr-manifest.sh IMAGE VERSION"
fi

image=$1
reference=$2
if [[ ! $image =~ ^ghcr\.io/[a-z0-9._/-]+$ ]] ||
   [[ $image == *//* ]] ||
   [[ $image == */ ]] ||
   [[ $image == *'/../'* ]]; then
  fail "The GHCR image name is invalid."
fi
if [[ ! $reference =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  fail "The GHCR version reference is invalid."
fi
if [[ ! ${GITHUB_ACTOR:-} =~ ^[A-Za-z0-9-]+$ ]] ||
   [[ ! ${GHCR_TOKEN:-} =~ ^[A-Za-z0-9._~+/-]+=*$ ]]; then
  fail "The GHCR credential environment is invalid."
fi

registry_base=https://ghcr.io
if [[ -n ${GHCR_REGISTRY_BASE:-} ]]; then
  if [[ ${GHCR_AUTH_TESTING:-} != true ]] ||
     [[ ! $GHCR_REGISTRY_BASE =~ ^http://127\.0\.0\.1:[1-9][0-9]{0,4}$ ]]; then
    fail "A registry override is permitted only for loopback contract tests."
  fi
  registry_base=$GHCR_REGISTRY_BASE
fi

repository_path=${image#ghcr.io/}
registry_authority=${registry_base#*://}
manifest_url="$registry_base/v2/$repository_path/manifests/$reference"
accept_header='Accept: application/vnd.oci.image.index.v1+json, application/vnd.docker.distribution.manifest.list.v2+json'

temporary_directory=$(mktemp -d)
trap 'rm -rf "$temporary_directory"' EXIT HUP INT TERM
umask 077
challenge_headers=$temporary_directory/challenge-headers
token_response=$temporary_directory/token-response
token_config=$temporary_directory/token-config
bearer_config=$temporary_directory/bearer-config
manifest_headers=$temporary_directory/manifest-headers

if ! challenge_status=$(curl --silent --show-error \
    --ignore-content-length \
    --connect-timeout 5 \
    --max-time 20 \
    --dump-header "$challenge_headers" \
    --output /dev/null \
    --write-out '%{http_code}' \
    --request HEAD \
    --header "$accept_header" \
    "$manifest_url"); then
  fail "The registry challenge probe failed."
fi
if [[ $challenge_status != 401 ]]; then
  fail "The registry challenge probe failed with unexpected HTTP status $challenge_status."
fi

mapfile -t challenges < <(
  awk '
    BEGIN { IGNORECASE = 1 }
    /^www-authenticate:[[:space:]]*/ {
      sub(/^[^:]+:[[:space:]]*/, "")
      sub(/\r$/, "")
      print
    }
  ' "$challenge_headers"
)
if [[ ${#challenges[@]} -ne 1 ]]; then
  fail "The registry authentication challenge was missing or ambiguous."
fi

challenge_pattern='^Bearer[[:space:]]+(.+)$'
shopt -s nocasematch
if [[ ! ${challenges[0]} =~ $challenge_pattern ]]; then
  fail "The registry authentication challenge was malformed."
fi
challenge_parameters=${BASH_REMATCH[1]}
shopt -u nocasematch
realm=
service=
scope=
realm_seen=0
service_seen=0
scope_seen=0
parameter_pattern='^([A-Za-z][A-Za-z0-9_-]*)[[:space:]]*=[[:space:]]*"([^"\\]*)"[[:space:]]*(,[[:space:]]*(.*))?$'
while [[ -n $challenge_parameters ]]; do
  if [[ ! $challenge_parameters =~ $parameter_pattern ]]; then
    fail "The registry authentication challenge was malformed."
  fi
  parameter_name=${BASH_REMATCH[1],,}
  parameter_value=${BASH_REMATCH[2]}
  challenge_parameters=${BASH_REMATCH[4]:-}
  case $parameter_name in
    realm)
      if (( realm_seen )); then
        fail "The registry authentication challenge contained a duplicate realm."
      fi
      realm=$parameter_value
      realm_seen=1
      ;;
    service)
      if (( service_seen )); then
        fail "The registry authentication challenge contained a duplicate service."
      fi
      service=$parameter_value
      service_seen=1
      ;;
    scope)
      if (( scope_seen )); then
        fail "The registry authentication challenge contained a duplicate scope."
      fi
      scope=$parameter_value
      scope_seen=1
      ;;
    *)
      ;;
  esac
done
if (( realm_seen != 1 || service_seen != 1 || scope_seen != 1 )); then
  fail "The registry authentication challenge omitted a required parameter."
fi
if [[ $realm != "$registry_base/token" ]] ||
   [[ $service != "$registry_authority" ]] ||
   [[ $scope != "repository:$repository_path:pull" ]]; then
  fail "The registry authentication challenge did not match the requested repository."
fi

printf 'user = "%s:%s"\n' "$GITHUB_ACTOR" "$GHCR_TOKEN" > "$token_config"
if ! token_status=$(curl --silent --show-error \
    --connect-timeout 5 \
    --max-time 20 \
    --config "$token_config" \
    --get \
    --data-urlencode "service=$service" \
    --data-urlencode "scope=$scope" \
    --output "$token_response" \
    --write-out '%{http_code}' \
    "$realm"); then
  fail "The registry token exchange failed."
fi
if [[ $token_status != 200 ]]; then
  fail "The registry token exchange failed with unexpected HTTP status $token_status."
fi
if ! bearer_token=$(jq -er '(.token // .access_token) | select(type == "string")' "$token_response" 2>/dev/null) ||
   [[ ! $bearer_token =~ ^[A-Za-z0-9._~+/-]+=*$ ]]; then
  fail "The registry token response was malformed."
fi

printf 'header = "Authorization: Bearer %s"\n' "$bearer_token" > "$bearer_config"
if ! manifest_status=$(curl --silent --show-error \
    --ignore-content-length \
    --connect-timeout 5 \
    --max-time 20 \
    --config "$bearer_config" \
    --dump-header "$manifest_headers" \
    --output /dev/null \
    --write-out '%{http_code}' \
    --request HEAD \
    --header "$accept_header" \
    "$manifest_url"); then
  fail "The authenticated manifest probe failed."
fi

case $manifest_status in
  404)
    printf '%s\n' absent
    ;;
  200)
    mapfile -t digests < <(
      awk '
        BEGIN { IGNORECASE = 1 }
        /^docker-content-digest:[[:space:]]*/ {
          sub(/^[^:]+:[[:space:]]*/, "")
          sub(/\r$/, "")
          print
        }
      ' "$manifest_headers"
    )
    if [[ ${#digests[@]} -ne 1 ]] ||
       ! "$(dirname -- "$0")/validate-sha256-digest.sh" "${digests[0]}"; then
      fail "The authenticated manifest response did not contain one valid digest."
    fi
    printf 'present %s\n' "${digests[0]}"
    ;;
  *)
    fail "The authenticated manifest probe failed with unexpected HTTP status $manifest_status."
    ;;
esac
