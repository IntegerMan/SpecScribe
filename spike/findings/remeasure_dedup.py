#!/usr/bin/env python3
"""Re-measurement of Story 25.3's corpus and sizing figures, WITH deduplication.

Why this exists
---------------
The code review of Story 25.3 (2026-08-07) found that `map_to_model.py` and
`measure_channels.py` union the SonarCloud observation set and the raw-Roslyn-SARIF
observation set with no deduplication:

    obs = [from_sonar(i) for i in sonar] + [from_sarif(r, ...) for r, ... in sarif]

giving N = 2,300. But section 1.1 of the spike report itself establishes that ~819 of the
834 raw SARIF results are the SAME defects SonarCloud already imported as
`external_roslyn:*` (CA1861: 339 raw vs 338 Sonar). So the union double-counts, and every
attachment ratio and digest-sizing figure derived from it is inflated.

The review ALSO found an error in the OPPOSITE direction: the record `measure_channels.py`
sizes is not the record ADR 0023 specifies. It omits the mandatory `attachment` and
`provenance` blocks entirely, and carries `rule.name`/`rule.helpUri` = None on every Sonar
row by construction. So the byte figures are simultaneously too high (double-count) and too
low (truncated record), and the net error was unknown.

This script fixes both directions and reports each separately, so the effect of each is
visible rather than netted out.

Honesty about what this can and cannot restore
----------------------------------------------
It CANNOT reconstruct the 2026-07-28 numbers. `sonar_p1..3.json` was never committed, and
`api/issues/search?resolved=false` is an as-of-now query against a backlog the spike itself
measured growing ~50/day. This is therefore a FRESH measurement at today's revision, and it
is labelled as such everywhere. The raw SARIF half IS the committed 2026-07-28 evidence.

Usage
-----
    curl -s "https://sonarcloud.io/api/issues/search?componentKeys=IntegerMan_SpecScribe\
&resolved=false&ps=500&p=N" -o <scratch>/sonar_pN.json      # for every page, not just 3
    python spike/findings/remeasure_dedup.py <scratch>

Unlike `map_to_model.py`, this script paginates to exhaustion rather than assuming three
pages, and refuses to report a total it knows is truncated.
"""
import json
import os
import sys
import glob
import statistics

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))

SEVERITY = ["none", "note", "warning", "error"]
SONAR_MQR_TO_NORM = {"BLOCKER": "error", "HIGH": "error", "MEDIUM": "warning",
                     "LOW": "note", "INFO": "note"}
SARIF_LEVEL_TO_NORM = {"error": "error", "warning": "warning", "note": "note",
                       "none": "none"}
SEVERITY_LABEL = {"error": "Error", "warning": "Warning", "note": "Note", "none": "None"}


def load_sonar(scratch):
    """Load every sonar page present, and verify the set is COMPLETE against `total`.

    map_to_model.py looped `for p in (1, 2, 3)`, a hard 1,500-issue cap that the live
    backlog (1,755 on 2026-08-07) has since passed. A truncated corpus reported as
    complete is the failure this guard exists to prevent.
    """
    pages = sorted(glob.glob(os.path.join(scratch, "sonar_p*.json")))
    if not pages:
        # Fall back to the committed minimal snapshot so the measurement is reproducible
        # without network access. The code review found that the Sonar half of the
        # original evidence shipped with no payload at all, which made every figure
        # derived from it unreproducible; this is the fix.
        snap = os.path.join(HERE, "sonar-snapshot-2026-08-07.json")
        if os.path.exists(snap):
            print("[no sonar_p*.json in %s -- using the committed snapshot %s]"
                  % (scratch, os.path.basename(snap)))
            pages = [snap]
        else:
            sys.exit("No sonar_p*.json in %s and no committed snapshot -- fetch them "
                     "first (see module docstring)." % scratch)
    issues, total = [], None
    for f in pages:
        with open(f, encoding="utf-8") as fh:
            d = json.load(fh)
        if "issues" not in d:
            sys.exit("%s has no 'issues' key -- an API error body, not a result page." % f)
        issues += d["issues"]
        total = (d.get("paging") or {}).get("total", total)
    if total is not None and len(issues) != total:
        sys.exit("INCOMPLETE: loaded %d issues but the API reports total=%d. Fetch the "
                 "missing pages; this script will not report a truncated corpus as whole."
                 % (len(issues), total))
    return issues, total


def sonar_key(issue):
    """Dedup key for a Sonar issue: (bare rule id, repo-relative path, start line).

    `external_roslyn:CA1861` and raw SARIF's `CA1861` are the same rule; the repo prefix is
    the only difference. Sonar's `component` is `PROJECT:path`.
    """
    rule = (issue.get("rule") or "")
    bare = rule.split(":", 1)[1] if ":" in rule else rule
    comp = issue.get("component") or ""
    path = comp.split(":", 1)[1] if ":" in comp else None
    tr = issue.get("textRange") or {}
    line = tr.get("startLine") or issue.get("line")
    return (bare, path, line)


def sarif_key(result, rules):
    rid = result.get("ruleId")
    if not rid:
        idx = result.get("ruleIndex")
        if idx is not None and 0 <= idx < len(rules):
            rid = rules[idx].get("id")
    locs = result.get("locations") or []
    path = line = None
    if locs:
        pl = locs[0].get("physicalLocation", {})
        uri = pl.get("artifactLocation", {}).get("uri", "") or ""
        p = uri[len("file:///"):] if uri.startswith("file:///") else uri
        try:
            from urllib.parse import unquote
            p = unquote(p)
        except Exception:
            pass
        try:
            path = os.path.relpath(p, REPO).replace("\\", "/")
        except ValueError:
            path = None            # cross-drive: do NOT fall back to the absolute path
        # The SARIF was built in the MAIN checkout; this script may run from a worktree
        # three levels below it, so relpath yields '../../../src/...'. Strip the ascent
        # rather than leaving a path that matches nothing -- and never fall back to the
        # absolute build-machine path, which is the leak ADR 0023 Decision 4 forbids.
        if path:
            while path.startswith("../"):
                path = path[3:]
        line = (pl.get("region") or {}).get("startLine")
    return (rid, path, line)


def load_sarif():
    out = []
    for name in ("roslyn-specscribe.sarif", "roslyn-tests.sarif"):
        f = os.path.join(HERE, name)
        if not os.path.exists(f):
            sys.exit("missing committed evidence: %s" % f)
        with open(f, encoding="utf-8") as fh:
            d = json.load(fh)
        for run in d.get("runs", []):
            rules = (run.get("tool", {}).get("driver", {}) or {}).get("rules", []) or []
            for r in run.get("results", []) or []:
                out.append((r, rules))
    return out


def sized_record(path, norm, provider_sev, message, rule_id, rule_name, help_uri,
                 full):
    """One AnalysisObservation. `full=False` reproduces what measure_channels.py sized;
    `full=True` is what ADR 0023 actually specifies."""
    rec = {
        "provider": "sonarcloud",
        "rule": {"id": rule_id, "name": rule_name, "helpUri": help_uri},
        "severity": {"normalized": norm, "label": SEVERITY_LABEL[norm],
                     "provider": provider_sev},
        "location": {"path": path, "startLine": 1, "startColumn": 1,
                     "endLine": 1, "endColumn": 20},
        "relatedLocations": [],
        "message": message,
    }
    if full:
        # ADR 0023 Decision 5 + Decision 6 -- both MANDATORY, neither sized before.
        rec["attachment"] = {"basis": "deep-git-commit-mining", "confidence": "approximate",
                             "epics": ["epic-24", "epic-25"],
                             "stories": ["24-1", "24-2", "25-1"], "entityCount": 2}
        rec["provenance"] = {"provider": "sonarcloud",
                             "analysisRevision": "d1722f17c0a1b2e3d4f5a6b7c8d9e0f1a2b3c4d5",
                             "analysisDate": "2026-08-07T12:00:00+0000",
                             "workingTreeRevision": "c954a72e1f2a3b4c5d6e7f8091a2b3c4d5e6f708",
                             "isStale": True, "commitsBehind": 2}
    return rec


def main():
    scratch = sys.argv[1] if len(sys.argv) > 1 else sys.exit("usage: remeasure_dedup.py <scratch>")

    sonar, total = load_sonar(scratch)
    sarif = load_sarif()

    print("=" * 78)
    print("RE-MEASUREMENT WITH DEDUPLICATION")
    print("=" * 78)
    print("Sonar issues (live, complete, verified against paging.total): %d" % len(sonar))
    print("Raw Roslyn SARIF results (committed 2026-07-28 evidence)    : %d" % len(sarif))
    print("Naive union as shipped by map_to_model.py                   : %d"
          % (len(sonar) + len(sarif)))

    # ---- the dedup measurement -------------------------------------------
    skeys = {}
    for i in sonar:
        skeys.setdefault(sonar_key(i), []).append(i)
    ext = [i for i in sonar if (i.get("rule") or "").startswith("external_roslyn:")]

    matched, unmatched = [], []
    for r, rules in sarif:
        if sarif_key(r, rules) in skeys:
            matched.append(r)
        else:
            unmatched.append((r, rules))

    print("\n" + "-" * 78)
    print("OVERLAP (exact rule+path+line match)")
    print("-" * 78)
    print("Sonar issues that are external_roslyn:* imports : %d" % len(ext))
    print("Raw SARIF results ALSO present in Sonar         : %d" % len(matched))
    print("Raw SARIF results NOT in Sonar (genuinely new)  : %d" % len(unmatched))
    print(">>> DISTINCT population = %d Sonar + %d unmatched SARIF = %d"
          % (len(sonar), len(unmatched), len(sonar) + len(unmatched)))
    dup = len(sonar) + len(sarif) - (len(sonar) + len(unmatched))
    print(">>> The naive union DOUBLE-COUNTS %d observations (%.1f%% inflation)"
          % (dup, 100.0 * dup / (len(sonar) + len(unmatched))))

    # ---- sizing, both error directions separated -------------------------
    print("\n" + "-" * 78)
    print("DIGEST SIZING -- each correction applied separately")
    print("-" * 78)

    def build(dedup, full):
        recs = []
        for i in sonar:
            imp = i.get("impacts") or []
            if imp:
                norm = max((SONAR_MQR_TO_NORM.get(x["severity"], "note") for x in imp),
                           key=SEVERITY.index)
            else:
                norm = "note"
            prov = [{"axis": "mqr", "softwareQuality": x.get("softwareQuality"),
                     "severity": x.get("severity")} for x in imp]
            prov.append({"axis": "legacy", "severity": i.get("severity"),
                         "type": i.get("type")})
            comp = i.get("component") or ""
            path = comp.split(":", 1)[1] if ":" in comp else None
            # rule.name/helpUri: None reproduces the shipped sizing; a realistic value
            # reproduces what the contract actually emits.
            rn = None if not full else "Sonar rule %s" % (i.get("rule") or "")
            hu = None if not full else \
                "https://rules.sonarsource.com/csharp/RSPEC-0000/%s" % (i.get("rule") or "")
            recs.append(sized_record(path, norm, prov, i.get("message"),
                                     i.get("rule"), rn, hu, full))
        src = unmatched if dedup else sarif
        for r, rules in src:
            lvl = SARIF_LEVEL_TO_NORM.get(r.get("level", "warning"), "warning")
            k = sarif_key(r, rules)
            rn = None if not full else "Roslyn rule %s" % (k[0] or "")
            hu = None if not full else "https://learn.microsoft.com/dotnet/fundamentals/" \
                                       "code-analysis/quality-rules/%s" % (k[0] or "")
            recs.append(sized_record(k[1], lvl,
                                     [{"axis": "sarif", "level": r.get("level")}],
                                     ((r.get("message") or {}).get("text")),
                                     k[0], rn, hu, full))
        return recs

    for label, dedup, full in (
            ("AS SHIPPED  (union, truncated record)", False, False),
            ("dedup only  (distinct, truncated record)", True, False),
            ("record only (union, FULL ADR-0023 record)", False, True),
            (">>> BOTH    (distinct, FULL ADR-0023 record)", True, True)):
        recs = build(dedup, full)
        blob = json.dumps(recs, separators=(",", ":"))
        per = len(blob) / float(len(recs))
        byfile = {}
        for o in recs:
            byfile.setdefault(o["location"]["path"] or "(none)", []).append(o)
        shards = [len(json.dumps(v, separators=(",", ":"))) for v in byfile.values()]
        index = json.dumps(sorted(byfile.keys()), separators=(",", ":"))
        print("\n%s" % label)
        print("  observations   : %d" % len(recs))
        print("  whole digest   : %s B (%.2f MB)" % (f"{len(blob):,}", len(blob) / 1048576.0))
        print("  bytes/obs      : %.0f" % per)
        print("  shards         : %d   index %s B   median shard %s B   max %s B"
              % (len(byfile), f"{len(index):,}",
                 f"{int(statistics.median(shards)):,}", f"{max(shards):,}"))

    # ---- the 2.6x claim, like for like -----------------------------------
    print("\n" + "-" * 78)
    print("SARIF VERBOSITY -- the 2.6x claim, compared like for like")
    print("-" * 78)
    raw_on_disk = sum(os.path.getsize(os.path.join(HERE, n))
                      for n in ("roslyn-specscribe.sarif", "roslyn-tests.sarif"))
    minified = 0
    for name in ("roslyn-specscribe.sarif", "roslyn-tests.sarif"):
        with open(os.path.join(HERE, name), encoding="utf-8") as fh:
            minified += len(json.dumps(json.load(fh), separators=(",", ":")))
    print("raw SARIF on disk (INDENTED, as the report measured): %s B -> %.0f B/result"
          % (f"{raw_on_disk:,}", raw_on_disk / float(len(sarif))))
    print("raw SARIF MINIFIED (like-for-like vs the observation): %s B -> %.0f B/result"
          % (f"{minified:,}", minified / float(len(sarif))))
    print("indentation alone accounts for %.1f%% of the on-disk SARIF"
          % (100.0 * (raw_on_disk - minified) / raw_on_disk))


if __name__ == "__main__":
    main()
