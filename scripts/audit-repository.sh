#!/bin/sh
set -eu

repository_root=${1:-$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)}
git -C "$repository_root" rev-parse --is-inside-work-tree >/dev/null 2>&1 || {
  printf '%s\n' "Repository audit requires a Git worktree." >&2
  exit 1
}

failures=0
report()
{
  printf '%s\n' "$1" >&2
  failures=$((failures + 1))
}

repository_files=$(git -C "$repository_root" ls-files --cached --others --exclude-standard)

if printf '%s\n' "$repository_files" | grep -Eq '(^|/)(bin|obj|TestResults|\.vs)/|\.(dll|exe|pdb|trx|nupkg|snupkg)$'; then
  report "Tracked or release-candidate build artifact found."
fi

if printf '%s\n' "$repository_files" | grep -Eiq '(^|/)(\.env($|\.)|id_(rsa|dsa|ecdsa|ed25519)($|\.)|[^/]+\.(pem|pfx|p12|key))$'; then
  report "Secret-bearing filename found."
fi

cd "$repository_root"

credential_values=$(mktemp)
trap 'rm -f "$credential_values"' EXIT HUP INT TERM
credential_scan_status=0
credential_scanner=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)/find-credential-values.awk
git ls-files -z --cached --others --exclude-standard |
  xargs -0 awk -f "$credential_scanner" > "$credential_values" || credential_scan_status=$?

if [ "$credential_scan_status" -ne 0 ]; then
  report "Repository-wide credential scan could not complete."
else
  credential_failure=0
  while IFS= read -r credential_value; do
    case "$credential_value" in
      inventory-test-api-key|fixture-api-key-never-output|prompt-contract-test|composite-contract-test|container-smoke-fixture-key|container-smoke-auth-token)
        ;;
      *)
        credential_failure=1
        ;;
    esac
  done < "$credential_values"
  if [ "$credential_failure" -ne 0 ]; then
    report "Secret-looking credential assignment found."
  fi
fi

if grep -Eirl \
  'OpenTelemetry|ApplicationInsights|TelemetryClient|Sentry|Datadog|NewRelic|Mixpanel|Segment\.Analytics' \
  src Directory.Packages.props \
  --include='*.cs' --include='*.csproj' --include='*.props' --include='packages.lock.json' \
  --exclude-dir=bin --exclude-dir=obj 2>/dev/null | grep -q .; then
  report "Telemetry package or runtime hook found."
fi

if grep -Erho "https?://[^\"'[:space:])>]+" src --include='*.cs' --exclude-dir=bin --exclude-dir=obj 2>/dev/null | grep -Ev '^https://api\.hevyapp\.com/?$' | grep -q .; then
  report "Non-Hevy runtime origin found in production source."
fi

if grep -Eirl \
  'TODO|FIXME|TBD|HACK|NotImplementedException|PLACEHOLDER' \
  src scripts Dockerfile .github/workflows README.md SECURITY.md CONTRIBUTING.md \
  --exclude='audit-repository.sh' --exclude-dir=bin --exclude-dir=obj 2>/dev/null | grep -q .; then
  report "Deferred placeholder marker found in release content."
fi

comment_scanner=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)/detect-csharp-comments.awk
if find src tests -type d \( -name bin -o -name obj \) -prune -o -type f -name '*.cs' -exec awk -f "$comment_scanner" {} + 2>/dev/null | grep -q .; then
  report "C# single-line comment found; retain only essential XML documentation."
fi

if find tests -type d \( -name bin -o -name obj \) -prune -o -type f -name '*.cs' -exec grep -En '(^|[^[:alnum:]_])Assert\.' {} + 2>/dev/null | grep -q .; then
  report "Non-FluentAssertions assertion found."
fi

[ "$failures" -eq 0 ]
