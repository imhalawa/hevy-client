function quote_run(text, start,    count) {
  count = 0
  while (substr(text, start + count, 1) == "\"") count++
  return count
}

function report_comment() {
  print FILENAME ":" FNR
  found = 1
}

FNR == 1 {
  mode = "code"
  raw_delimiter = 0
}

{
  line = $0
  length_of_line = length(line)
  position = 1

  while (position <= length_of_line) {
    character = substr(line, position, 1)
    next_character = substr(line, position + 1, 1)

    if (mode == "raw") {
      if (character == "\"" && quote_run(line, position) >= raw_delimiter) {
        position += raw_delimiter
        mode = "code"
      } else {
        position++
      }
      continue
    }

    if (mode == "string") {
      if (character == "\\") {
        position += 2
      } else if (character == "\"") {
        position++
        mode = "code"
      } else {
        position++
      }
      continue
    }

    if (mode == "verbatim") {
      if (character == "\"" && next_character == "\"") {
        position += 2
      } else if (character == "\"") {
        position++
        mode = "code"
      } else {
        position++
      }
      continue
    }

    if (mode == "character") {
      if (character == "\\") {
        position += 2
      } else if (character == "'") {
        position++
        mode = "code"
      } else {
        position++
      }
      continue
    }

    if (character == "/" && next_character == "/") {
      prefix = substr(line, 1, position - 1)
      if (substr(line, position + 2, 1) == "/" && prefix ~ /^[[:space:]]*$/) break
      report_comment()
      break
    }

    if (character == "@" && next_character == "\"") {
      mode = "verbatim"
      position += 2
      continue
    }

    if (character == "\"") {
      run = quote_run(line, position)
      if (run >= 3) {
        mode = "raw"
        raw_delimiter = run
        position += run
      } else {
        mode = "string"
        position++
      }
      continue
    }

    if (character == "'") {
      mode = "character"
      position++
      continue
    }

    position++
  }
}

END {
  exit found ? 0 : 1
}
