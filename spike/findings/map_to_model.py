#!/usr/bin/env python3
"""Story 25.3 spike evidence — project a real SonarCloud issue and a real raw-Roslyn SARIF
result into ONE candidate model, and report what each direction loses.

THROWAWAY. Referenced by no .slnx, no build, no shipped code path. See spike/README.md.

Usage:  python spike/findings/map_to_model.py <dir-with-sonar_p*.json>
The SARIF inputs are read from spike/findings/ beside this script.
"""
import json
import os
import subprocess
import sys
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))

# ---------------------------------------------------------------------------
# The candidate model. One record, two producers.
#
# Naming (R2): "Finding" is taken -- `## Review Findings` is a PARSED story section
# (EpicsParser.cs:253) rendered as a visible <h3> on every story page
# (HtmlRenderAdapter.Epics.cs:609). "Insight" is taken (Git Insights, the Insights tab).
# "Coverage" is taken (ArtifactCoverage). We use OBSERVATION.
# ---------------------------------------------------------------------------

# Normalized 4-level ordinal scale. Chosen to match SARIF's `level` cardinality exactly so
# the SARIF direction is lossless; Sonar's 5-level scales collapse INTO it and the raw
# provider value rides alongside so nothing is destroyed.
SEVERITY = ["none", "note", "warning", "error"]  # ascending

SONAR_MQR_TO_NORM = {
    "BLOCKER": "error",
    "HIGH": "error",
    "MEDIUM": "warning",
    "LOW": "note",
    "INFO": "note",
}
SONAR_LEGACY_TO_NORM = {
    "BLOCKER": "error",
    "CRITICAL": "error",
    "MAJOR": "warning",
    "MINOR": "note",
    "INFO": "note",
}
SARIF_LEVEL_TO_NORM = {  # identity -- the scale was chosen to make it so
    "error": "error",
    "warning": "warning",
    "note": "note",
    "none": "none",
}

# Human text label per normalized level. UX-DR17: severity is NEVER color-alone, so the
# label is part of the contract, not a rendering choice.
SEVERITY_LABEL = {
    "error": "Error",
    "warning": "Warning",
    "note": "Note",
    "none": "None",
}


def observation(provider, rule_id, rule_name, help_uri, severity, provider_severity,
                path, start_line, start_col, end_line, end_col, message,
                related, raw_keys_dropped):
    return {
        "provider": provider,
        "rule": {"id": rule_id, "name": rule_name, "helpUri": help_uri},
        "severity": {
            "normalized": severity,
            "label": SEVERITY_LABEL[severity],
            "provider": provider_severity,   # verbatim, un-normalized, possibly a list
        },
        "location": {
            "path": path,                    # ALWAYS repo-relative, forward-slashed
            "startLine": start_line, "startColumn": start_col,
            "endLine": end_line, "endColumn": end_col,
        },
        "relatedLocations": related,
        "message": message,
        "_lost": raw_keys_dropped,
    }


# ---------------------------------------------------------------------------
# Direction 1: SonarCloud issue -> Observation
# ---------------------------------------------------------------------------
def from_sonar(issue):
    lost = []
    # component is "PROJECT:path" -- must be split. A component with no ':' is a project-level
    # issue with no file at all.
    comp = issue.get("component", "")
    path = comp.split(":", 1)[1] if ":" in comp else None
    if path is None:
        lost.append("component (project-level issue, no file path)")

    tr = issue.get("textRange") or {}

    # flows[] is flows-OF-locations (two levels). Flatten to a single related list.
    related = []
    for flow in issue.get("flows", []):
        for loc in flow.get("locations", []):
            lc = loc.get("component", "")
            ltr = loc.get("textRange") or {}
            related.append({
                "path": lc.split(":", 1)[1] if ":" in lc else lc,
                "startLine": ltr.get("startLine"),
                "startColumn": ltr.get("startOffset"),
                "endLine": ltr.get("endLine"),
                "endColumn": ltr.get("endOffset"),
                "message": loc.get("msg"),
            })

    impacts = issue.get("impacts", [])
    # The ARRAY question (R5): keep every pair verbatim; derive the normalized level from
    # the MAX so a multi-impact issue can never normalize below its worst quality.
    if impacts:
        norm = max((SONAR_MQR_TO_NORM.get(i["severity"], "note") for i in impacts),
                   key=SEVERITY.index)
        prov = [{"axis": "mqr", "softwareQuality": i["softwareQuality"],
                 "severity": i["severity"]} for i in impacts]
        prov.append({"axis": "legacy", "severity": issue.get("severity"),
                     "type": issue.get("type")})
    else:
        norm = SONAR_LEGACY_TO_NORM.get(issue.get("severity"), "note")
        prov = [{"axis": "legacy", "severity": issue.get("severity"),
                 "type": issue.get("type")}]
        lost.append("impacts[] absent -- fell back to the FROZEN legacy axis")

    for k in ("effort", "debt"):
        if issue.get(k):
            lost.append("%s=%s (no SpecScribe analogue)" % (k, issue[k]))
    if issue.get("assignee"):
        lost.append("assignee (DELIBERATELY dropped -- no people scoreboard)")
    if issue.get("hash"):
        lost.append("hash (Sonar's line-content hash, not portable)")
    if issue.get("key"):
        lost.append("key (server-assigned, NOT stable across re-analysis of a moved line)")
    if issue.get("tags"):
        lost.append("tags=%s" % issue["tags"])
    if issue.get("cleanCodeAttribute"):
        lost.append("cleanCodeAttribute/%s (MQR taxonomy, no SARIF or Roslyn analogue)"
                    % issue.get("cleanCodeAttributeCategory"))

    return observation(
        provider="sonarcloud",
        rule_id=issue.get("rule"),          # already "{repo}:{id}"
        rule_name=None,                     # requires a SECOND call: api/rules/show
        help_uri=None,                      # ditto
        severity=norm, provider_severity=prov,
        path=path,
        start_line=tr.get("startLine") or issue.get("line"),
        start_col=tr.get("startOffset"),
        end_line=tr.get("endLine"), end_col=tr.get("endOffset"),
        message=issue.get("message"),
        related=related, raw_keys_dropped=lost,
    )


# ---------------------------------------------------------------------------
# Direction 2: raw Roslyn SARIF result -> Observation
# ---------------------------------------------------------------------------
def from_sarif(result, rules, repo_root):
    lost = []
    locs = result.get("locations", [])
    path = start_line = start_col = end_line = end_col = None
    if locs:
        pl = locs[0].get("physicalLocation", {})
        uri = pl.get("artifactLocation", {}).get("uri", "")
        # ABSOLUTE file:// URI carrying the BUILD MACHINE's path. Must be re-rooted.
        p = uri
        if p.startswith("file:///"):
            p = p[len("file:///"):]
        p = p.replace("%20", " ")
        try:
            path = os.path.relpath(p, repo_root).replace("\\", "/")
        except ValueError:
            path = p
        reg = pl.get("region", {})
        start_line, start_col = reg.get("startLine"), reg.get("startColumn")
        end_line, end_col = reg.get("endLine"), reg.get("endColumn")
    else:
        lost.append("no location (compiler-level result)")

    # Rule metadata is OUT OF LINE: the result carries only ruleIndex into tool.driver.rules.
    idx = result.get("ruleIndex")
    rule = rules[idx] if idx is not None and idx < len(rules) else {}
    if not rule:
        lost.append("ruleIndex did not resolve -- rule metadata unavailable")

    related = []
    for rl in result.get("relatedLocations", []):
        pl = rl.get("physicalLocation", {})
        related.append({"path": pl.get("artifactLocation", {}).get("uri"),
                        "startLine": pl.get("region", {}).get("startLine")})
    for extra in locs[1:]:
        pl = extra.get("physicalLocation", {})
        related.append({"path": pl.get("artifactLocation", {}).get("uri"),
                        "startLine": pl.get("region", {}).get("startLine")})

    props = result.get("properties", {})
    if props.get("customProperties"):
        lost.append("properties.customProperties=%s" % props["customProperties"])
    if "warningLevel" in props:
        lost.append("properties.warningLevel=%s" % props["warningLevel"])
    rp = rule.get("properties", {}) if rule else {}
    if rp.get("executionTimeInSeconds"):
        lost.append("rule executionTime telemetry")
    if rp.get("category"):
        lost.append("rule category=%s (kept as a tag candidate)" % rp["category"])
    lost.append("NO Sonar analogue: cleanCodeAttribute, impacts[], effort/debt, issue key")

    return observation(
        provider="roslyn",
        rule_id="roslyn:%s" % result.get("ruleId"),
        rule_name=(rule.get("shortDescription") or {}).get("text"),
        help_uri=rule.get("helpUri"),
        severity=SARIF_LEVEL_TO_NORM.get(result.get("level", "warning"), "warning"),
        provider_severity=[{"axis": "sarif", "level": result.get("level"),
                            "defaultLevel": (rule.get("defaultConfiguration") or {}).get("level")}],
        path=path, start_line=start_line, start_col=start_col,
        end_line=end_line, end_col=end_col,
        message=(result.get("message") or {}).get("text"),
        related=related, raw_keys_dropped=lost,
    )


def main():
    scratch = sys.argv[1]
    sonar = []
    for p in (1, 2, 3):
        f = os.path.join(scratch, "sonar_p%d.json" % p)
        if os.path.exists(f):
            sonar += json.load(open(f))["issues"]

    sarif_results = []
    for f in ("roslyn-specscribe.sarif", "roslyn-tests.sarif"):
        d = json.load(open(os.path.join(HERE, f)))
        for run in d["runs"]:
            rules = run["tool"]["driver"].get("rules", [])
            for r in run.get("results", []):
                sarif_results.append((r, rules))

    print("=" * 78)
    print("INPUTS: %d live Sonar issues | %d raw Roslyn SARIF results"
          % (len(sonar), len(sarif_results)))
    print("=" * 78)

    son_obs = [from_sonar(i) for i in sonar]
    sar_obs = [from_sarif(r, rules, REPO) for r, rules in sarif_results]

    # ---- worked example, one from each direction -------------------------
    ex_s = next(o for o in son_obs if o["relatedLocations"])
    ex_r = sar_obs[0]
    print("\n--- WORKED EXAMPLE 1: SonarCloud -> Observation ---")
    print(json.dumps(ex_s, indent=1)[:1700])
    print("\n--- WORKED EXAMPLE 2: raw Roslyn SARIF -> Observation ---")
    print(json.dumps(ex_r, indent=1)[:1700])

    # ---- aggregate loss + collapse measurements --------------------------
    print("\n" + "=" * 78)
    print("SEVERITY COLLAPSE, measured on all %d Sonar issues" % len(son_obs))
    print("=" * 78)
    mqr = Counter(i["impacts"][0]["severity"] for i in sonar if i.get("impacts"))
    leg = Counter(i.get("severity") for i in sonar)
    norm_from_mqr = Counter(o["severity"]["normalized"] for o in son_obs)
    print("Sonar MQR      :", dict(mqr))
    print("Sonar legacy   :", dict(leg))
    print("NORMALIZED (from MQR):", dict(norm_from_mqr))
    alt = Counter(SONAR_LEGACY_TO_NORM.get(i.get("severity"), "note") for i in sonar)
    print("NORMALIZED (if legacy axis had been used instead):", dict(alt))
    disagree = sum(1 for i in sonar
                   if i.get("impacts") and
                   SONAR_MQR_TO_NORM.get(i["impacts"][0]["severity"]) !=
                   SONAR_LEGACY_TO_NORM.get(i.get("severity")))
    print(">>> issues whose NORMALIZED level DIFFERS by axis: %d of %d (%.1f%%)"
          % (disagree, len(sonar), 100.0 * disagree / len(sonar)))

    print("\nBLOCKER/HIGH merge cost (both -> 'error'):",
          dict(Counter(i["impacts"][0]["severity"] for i in sonar
                       if i.get("impacts") and i["impacts"][0]["severity"] in ("BLOCKER", "HIGH"))))

    print("\n" + "=" * 78)
    print("MULTI-LOCATION, measured")
    print("=" * 78)
    s_multi = sum(1 for o in son_obs if o["relatedLocations"])
    r_multi = sum(1 for o in sar_obs if o["relatedLocations"])
    print("Sonar issues carrying secondary locations : %d / %d (%.1f%%)"
          % (s_multi, len(son_obs), 100.0 * s_multi / len(son_obs)))
    print("  total secondary locations              : %d"
          % sum(len(o["relatedLocations"]) for o in son_obs))
    print("  max on a single issue                  : %d"
          % max(len(o["relatedLocations"]) for o in son_obs))
    print("Roslyn SARIF results carrying them       : %d / %d (%.1f%%)"
          % (r_multi, len(sar_obs), 100.0 * r_multi / len(sar_obs)))
    print(">>> multi-location is SOURCE-CLASS DEPENDENT, not a universal property.")

    print("\n" + "=" * 78)
    print("MULTI-IMPACT (the R5 array question), measured")
    print("=" * 78)
    print("impacts[] length distribution:",
          dict(Counter(len(i.get("impacts", [])) for i in sonar)))

    print("\n" + "=" * 78)
    print("LOSS LISTS")
    print("=" * 78)
    sl = Counter()
    for o in son_obs:
        for x in o["_lost"]:
            sl[x.split("=")[0].split(" (")[0]] += 1
    print("Sonar -> Observation, most common losses:")
    for k, v in sl.most_common(9):
        print("   %5d  %s" % (v, k))
    rl = Counter()
    for o in sar_obs:
        for x in o["_lost"]:
            rl[x.split("=")[0].split(" (")[0]] += 1
    print("Roslyn SARIF -> Observation, most common losses:")
    for k, v in rl.most_common(9):
        print("   %5d  %s" % (v, k))

    # ---- payload size ----------------------------------------------------
    print("\n" + "=" * 78)
    print("PAYLOAD SIZE (AC #3's digest row needs a real number)")
    print("=" * 78)
    slim = [{k: v for k, v in o.items() if k != "_lost"} for o in son_obs + sar_obs]
    blob = json.dumps(slim, separators=(",", ":"))
    print("all %d observations, compact JSON : %s bytes (%.2f MB), %.0f B/observation"
          % (len(slim), format(len(blob), ","), len(blob) / 1048576.0, len(blob) / len(slim)))
    raw = sum(os.path.getsize(os.path.join(HERE, f))
              for f in ("roslyn-specscribe.sarif", "roslyn-tests.sarif"))
    print("raw SARIF on disk for %d results   : %s bytes (%.2f MB), %.0f B/result"
          % (len(sar_obs), format(raw, ","), raw / 1048576.0, raw / len(sar_obs)))

    # ---- path normalization ---------------------------------------------
    print("\n" + "=" * 78)
    print("PATH NORMALIZATION -- both providers need it, in OPPOSITE directions")
    print("=" * 78)
    print("Sonar  raw: %s" % sonar[0].get("component"))
    print("Sonar  ->  : %s" % son_obs[0]["location"]["path"])
    raw_uri = (sarif_results[0][0].get("locations") or [{}])[0] \
        .get("physicalLocation", {}).get("artifactLocation", {}).get("uri")
    print("SARIF  raw: %s" % raw_uri)
    print("SARIF  ->  : %s" % sar_obs[0]["location"]["path"])
    bad = sum(1 for o in sar_obs if o["location"]["path"] and o["location"]["path"].startswith(".."))
    print(">>> SARIF results resolving OUTSIDE the repo root after re-rooting: %d" % bad)

    # ---- overlap ---------------------------------------------------------
    print("\n" + "=" * 78)
    print("OVERLAP: is the second source class actually independent?")
    print("=" * 78)
    son_ext = Counter(i["rule"] for i in sonar if i["rule"].startswith("external_roslyn:"))
    sar_rules = Counter(o["rule"]["id"] for o in sar_obs)
    print("Sonar external_roslyn:* issues : %d" % sum(son_ext.values()))
    print("raw SARIF results              : %d" % len(sar_obs))
    for rid, n in sar_rules.most_common(6):
        bare = rid.split(":", 1)[1]
        print("   %-14s raw=%4d   sonar external_roslyn=%4d"
              % (bare, n, son_ext.get("external_roslyn:" + bare, 0)))

    # ---- staleness -------------------------------------------------------
    print("\n" + "=" * 78)
    print("STALENESS -- revision, not timestamp")
    print("=" * 78)
    af = os.path.join(scratch, "analyses.json")
    if os.path.exists(af):
        an = json.load(open(af))["analyses"][0]
        head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=REPO,
                              capture_output=True, text=True).stdout.strip()
        rev = an.get("revision")
        print("latest analysis date     : %s  <- looks fresh" % an.get("date"))
        print("latest analysis revision : %s" % rev)
        print("local working-tree HEAD  : %s" % head)
        if rev and head:
            behind = subprocess.run(["git", "rev-list", "--count", "%s..%s" % (rev, head)],
                                    cwd=REPO, capture_output=True, text=True).stdout.strip()
            print(">>> analysis is %s commit(s) BEHIND the working tree." % behind)
            print(">>> The timestamp says 'today'. The revision says stale. Only the revision is honest.")


if __name__ == "__main__":
    main()
