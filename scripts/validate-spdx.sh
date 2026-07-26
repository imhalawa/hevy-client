#!/bin/sh
set -eu

if [ "$#" -eq 0 ]; then
  printf '%s\n' "At least one SPDX document is required." >&2
  exit 1
fi

for document do
  if [ ! -f "$document" ] || ! jq -e '
      def nonblank: ((type == "string") and (length > 0));
      def spdx_id: ((type == "string") and test("^SPDXRef-[A-Za-z0-9.-]+$"));
      ([.packages[]?.SPDXID] + [.files[]?.SPDXID]) as $element_ids |
      (.SPDXID == "SPDXRef-DOCUMENT") and
      (.spdxVersion == "SPDX-2.3") and
      (.dataLicense == "CC0-1.0") and
      (.name | nonblank) and
      ((.documentNamespace | type) == "string") and
      (.documentNamespace | test("^https?://[^[:space:]]+$")) and
      ((.creationInfo | type) == "object") and
      ((.creationInfo.created | type) == "string") and
      (.creationInfo.created | test("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]+)?Z$")) and
      ((.creationInfo.creators | type) == "array") and
      ((.creationInfo.creators | length) > 0) and
      all(.creationInfo.creators[]; nonblank) and
      ((has("packages") | not) or
        (((.packages | type) == "array") and all(.packages[];
          (.SPDXID | spdx_id) and
          (.name | nonblank) and
          (.downloadLocation | nonblank) and
          (.filesAnalyzed | type == "boolean")))) and
      ((has("files") | not) or
        (((.files | type) == "array") and all(.files[];
          (.SPDXID | spdx_id) and
          (.fileName | nonblank)))) and
      (($element_ids | length) > 0 and
        ($element_ids | length) == ($element_ids | unique | length)) and
      ((.relationships | type) == "array") and
      ((.relationships | length) > 0) and
      all(.relationships[];
        (.spdxElementId | spdx_id) and
        (.relationshipType | nonblank) and
        (.relatedSpdxElement | spdx_id)) and
      any(.relationships[];
        .spdxElementId == "SPDXRef-DOCUMENT" and
        .relationshipType == "DESCRIBES" and
        (.relatedSpdxElement as $related | $element_ids | index($related) != null))
    ' "$document" >/dev/null; then
    printf '%s\n' "Every SBOM must be a complete SPDX-2.3 JSON document with an internally described subject." >&2
    exit 1
  fi
done

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  printf '%s\n' 'predicate_type=https://spdx.dev/Document/v2.3' >> "$GITHUB_OUTPUT"
else
  printf '%s\n' 'https://spdx.dev/Document/v2.3'
fi
