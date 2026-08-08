#!/usr/bin/env bash
# ADR 0040 §Decision 7 — SOURCE_DATE_EPOCH must be set and well-formed BEFORE anything is built.
# [Story 16.4 AC #3, Task 3]
#
# 🚨 THIS ASSERTION EXISTS BECAUSE THE FAILURE IS SILENT AND THE PIPELINE STAYS GREEN.
#
# SpecScribe.csproj:36-38 gates the variable on '^[0-9]{1,10}$' and, when it does not match, falls back to
# TODAY'S DATE rather than failing:
#
#     <_SourceDateEpochValid Condition="...Regex::IsMatch('$(SOURCE_DATE_EPOCH)', '^[0-9]{1,10}$')">true</...>
#     <SpecScribeBuildDate Condition="'$(_SourceDateEpochValid)' != 'true'">$([System.DateTime]::UtcNow...)</...>
#
# So an unset, misspelled or malformed variable stamps the BUILD date into the assembly, the About page shows a
# date that moves on every re-run, and nothing anywhere reports a problem. That is exactly why ADR 0040 calls
# specifying the value "load-bearing rather than pedantic" — the guard has to be here, on the outside.
#
# The value must be the TAGGED COMMIT'S committer timestamp (`git log -1 --format=%ct <sha>`), never the run's
# start time: a run-start value differs on every re-run and defeats the property it was set to establish.
#
# $1 : the value to check (defaults to $SOURCE_DATE_EPOCH).
set -euo pipefail

VALUE=${1-${SOURCE_DATE_EPOCH-}}

fail() {
  echo "FAIL — SOURCE_DATE_EPOCH $1" >&2
  echo >&2
  echo "       SpecScribe.csproj:36-38 does NOT fail on a bad value — it silently stamps today's date, so this" >&2
  echo "       release would produce an irreproducible artefact with a green pipeline. Set it from the tagged" >&2
  echo "       commit's committer timestamp:  SOURCE_DATE_EPOCH=\$(git log -1 --format=%ct \"\$SHA\")" >&2
  exit 1
}

[ -n "$VALUE" ] || fail "is empty or unset."

# The same regex the csproj uses, deliberately. A guard that accepts what its subject rejects is not a guard —
# 10 digits reaches year ~2286, which comfortably bounds any realistic epoch while keeping MSBuild's AddSeconds
# inside DateTime's representable range.
printf '%s' "$VALUE" | grep -Eq '^[0-9]{1,10}$' || fail "is '${VALUE}', which does not match ^[0-9]{1,10}\$."

echo "SOURCE_DATE_EPOCH OK — ${VALUE} ($(date -u -d "@${VALUE}" '+%Y-%m-%d' 2>/dev/null \
      || date -u -r "${VALUE}" '+%Y-%m-%d' 2>/dev/null || echo 'date conversion unavailable on this host'))"
