#!/usr/bin/env python3
"""Story 25.3 spike evidence — size the digest channel three ways (whole / sharded / index)
so AC #3's digest row carries a real number instead of a category.

THROWAWAY. See spike/README.md.
Usage: python spike/findings/measure_channels.py <scratch-dir-with-sonar_p*.json>
"""
import json
import os
import statistics
import sys
from collections import defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import map_to_model as M  # noqa: E402

REPO = M.REPO


def main():
    scratch = sys.argv[1]
    sonar = []
    for p in (1, 2, 3):
        f = os.path.join(scratch, "sonar_p%d.json" % p)
        if os.path.exists(f):
            sonar += json.load(open(f))["issues"]

    sar = []
    for f in ("roslyn-specscribe.sarif", "roslyn-tests.sarif"):
        d = json.load(open(os.path.join(M.HERE, f)))
        for run in d["runs"]:
            rules = run["tool"]["driver"].get("rules", [])
            for r in run.get("results", []):
                sar.append((r, rules))

    obs = [M.from_sonar(i) for i in sonar] + [M.from_sarif(r, ru, REPO) for r, ru in sar]
    for o in obs:
        o.pop("_lost", None)

    whole = json.dumps(obs, separators=(",", ":"))
    print("=" * 74)
    print("DIGEST CHANNEL, sized on %d real observations" % len(obs))
    print("=" * 74)
    print("WHOLE digest         : %s bytes (%.2f MB)"
          % (format(len(whole), ","), len(whole) / 1048576.0))

    byfile = defaultdict(list)
    for o in obs:
        byfile[o["location"]["path"] or "(none)"].append(o)
    sizes = [len(json.dumps(v, separators=(",", ":"))) for v in byfile.values()]
    print("SHARDED per file     : %d shards, %s bytes total"
          % (len(byfile), format(sum(sizes), ",")))
    print("   median shard %s B | mean %s B | max %s B (%.1f KB)"
          % (format(int(statistics.median(sizes)), ","),
             format(int(statistics.mean(sizes)), ","),
             format(max(sizes), ","), max(sizes) / 1024.0))

    idx = json.dumps({k: len(v) for k, v in byfile.items()}, separators=(",", ":"))
    print("INDEX (path->count)  : %s bytes (%.1f KB)"
          % (format(len(idx), ","), len(idx) / 1024.0))
    print()
    print(">>> The 25.4 use case ('the files I am about to touch') reads the index (%.1f KB)"
          % (len(idx) / 1024.0))
    print("    plus N shards at a median of %s B each -- NOT the %.2f MB whole digest."
          % (format(int(statistics.median(sizes)), ","), len(whole) / 1048576.0))
    print()
    print("Heaviest files:")
    for f, v in sorted(byfile.items(), key=lambda kv: -len(kv[1]))[:5]:
        print("   %-48s %3d observations" % (f, len(v)))


if __name__ == "__main__":
    main()
