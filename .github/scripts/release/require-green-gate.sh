#!/usr/bin/env bash
# ADR 0040 §Decision 9 — the release preflight's gate check. [Story 16.4 AC #1, Task 2]
#
# NFR9 requires publishing to be gated on a passing build+test run. That is satisfied by requiring the tagged
# commit to ALREADY be green on `main` — deliberately not by re-running the suite inside the release job
# ("re-running invites a different result from the same source and doubles the wall-clock"), and epics.md
# §Story 16.2 (AMENDED 2026-07-25) forbids creating a second build+test workflow outright.
#
# This is the NETWORK half; the decision lives in `gate-verdict.mjs` and is tested there. Keeping them apart is
# what makes the red paths reachable without a network or a deliberately-broken commit.
#
# $1 : owner/repo   $2 : commit SHA   $3 : check name (default: build-test-analyze)
# env: GH_TOKEN — required by `gh api`.
#      GATE_POLL_INTERVAL_SECONDS / GATE_POLL_TIMEOUT_SECONDS — overridable for tests only.
set -euo pipefail

REPO=${1:?usage: require-green-gate.sh <owner/repo> <sha> [check-name]}
SHA=${2:?usage: require-green-gate.sh <owner/repo> <sha> [check-name]}
CHECK_NAME=${3:-build-test-analyze}

# ADR 0040 §Decision 9 fixes both numbers: poll at 30 s intervals, up to 15 minutes.
INTERVAL_SECONDS=${GATE_POLL_INTERVAL_SECONDS:-30}
TIMEOUT_SECONDS=${GATE_POLL_TIMEOUT_SECONDS:-900}

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

echo "Gate: requiring '${CHECK_NAME}' to be green for ${REPO}@${SHA}"
echo "      polling every ${INTERVAL_SECONDS}s for up to ${TIMEOUT_SECONDS}s while it is still running"

deadline=$(( SECONDS + TIMEOUT_SECONDS ))
attempt=0

while :; do
  attempt=$(( attempt + 1 ))

  # --paginate matters: a commit accumulates one check run per re-run, and the newest can fall off page one.
  # A verdict computed from a truncated list would silently honour a SUPERSEDED green, which is precisely the
  # direction ADR 0040 §Decision 9 forbids.
  payload=$(gh api --paginate "repos/${REPO}/commits/${SHA}/check-runs" --jq '.check_runs[]' \
            | node -e 'const c=[];process.stdin.on("data",d=>c.push(d)).on("end",()=>{
                 const t=Buffer.concat(c).toString("utf8").trim();
                 process.stdout.write(JSON.stringify(t?t.split("\n").map(l=>JSON.parse(l)):[]));});')

  set +e
  verdict=$(printf '%s' "$payload" | node "${here}/gate-verdict.mjs" "$CHECK_NAME")
  status=$?
  set -e

  printf '%s\n' "$verdict"

  case "$status" in
    0) exit 0 ;;
    75)
      if [ "$SECONDS" -ge "$deadline" ]; then
        echo "FAIL — '${CHECK_NAME}' had still not completed after ${TIMEOUT_SECONDS}s (${attempt} polls)."
        echo "       Failing the release. Nothing has been published; re-run once the gate has finished."
        exit 1
      fi
      sleep "$INTERVAL_SECONDS"
      ;;
    *) exit "$status" ;;
  esac
done
