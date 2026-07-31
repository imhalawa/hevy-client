function emit(candidate,    value) {
  sub(/^[^:=]*[:=][[:space:]]*/, "", candidate)
  sub(/^["\047]/, "", candidate)
  if (match(candidate, /^[A-Za-z0-9+/=_.~-]+/)) {
    value = substr(candidate, RSTART, RLENGTH)
    if (length(value) >= 20) print value
  }
}

BEGIN {
  quote = "[\"\047]?"
  key = "(hevy_api_key|mcp_auth_token|api[-_]?key|auth(orization)?|bearer[-_]?token)"
  prefix = quote key quote "\\]?[[:space:]]*[:=][[:space:]]*"
  quoted = prefix "[\"\047][A-Za-z0-9+/=_.~-]+[\"\047]"
  unquoted = prefix "[A-Za-z0-9+/=_.~-]+[[:space:]]*(#.*)?$"
}

{
  rest = $0
  while (length(rest) > 0) {
    lower = tolower(rest)
    quoted_start = match(lower, quoted)
    quoted_length = RLENGTH
    unquoted_start = match(lower, unquoted)
    unquoted_length = RLENGTH

    if (quoted_start == 0 && unquoted_start == 0) break
    if (quoted_start > 0 && (unquoted_start == 0 || quoted_start <= unquoted_start)) {
      start = quoted_start
      matched_length = quoted_length
    } else {
      start = unquoted_start
      matched_length = unquoted_length
    }

    emit(substr(rest, start, matched_length))
    rest = substr(rest, start + matched_length)
  }
}
