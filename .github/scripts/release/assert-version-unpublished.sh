#!/usr/bin/env bash
# ADR 0040 §Decision 10's registry preflight — the network half. [Story 16.4 AC #2, Task 9]
# The decision lives in `nuget-version-consumed.mjs` and is tested there.
#
# $1 : nuget package id (e.g. SpecScribe)   $2 : version (e.g. 0.1.0-preview.1)
set -euo pipefail

PACKAGE_ID=${1:?usage: assert-version-unpublished.sh <package-id> <version>}
VERSION=${2:?usage: assert-version-unpublished.sh <package-id> <version>}

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

# The flat container ("package base address") is the right resource: it is the raw version list, it is CDN-backed
# rather than search-indexed, and — unlike the search API — it has no indexing delay. A version pushed a minute
# ago is visible here, which is what makes this check trustworthy immediately after a partial release.
lower=$(printf '%s' "$PACKAGE_ID" | tr '[:upper:]' '[:lower:]')
url="https://api.nuget.org/v3-flatcontainer/${lower}/index.json"

echo "nuget preflight: is ${PACKAGE_ID} ${VERSION} already consumed?  (${url})"

# --fail-with-body would swallow the 404 body; we WANT to distinguish 404 from a transport error, because they
# mean opposite things. 404 = the package id has never been published = every version is free.
http_status=$(curl --silent --show-error --location --max-time 30 \
                   --write-out '%{http_code}' --output /tmp/nuget-index.json "$url" || echo "000")

case "$http_status" in
  404)
    echo "  nuget.org has no registration for '${PACKAGE_ID}' (HTTP 404)."
    : > /tmp/nuget-index.json
    ;;
  200) ;;
  *)
    # Fail closed: an unreachable registry is not evidence that the version is free.
    echo "FAIL — could not read the nuget.org index (HTTP ${http_status})." >&2
    echo "       Refusing to treat an unreachable registry as 'this version is free' — the conflict would" >&2
    echo "       surface at the push step instead, after everything is built and the Release is drafted." >&2
    exit 1
    ;;
esac

node "${here}/nuget-version-consumed.mjs" "$VERSION" < /tmp/nuget-index.json
