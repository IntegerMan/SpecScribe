---
stepsCompleted: [1, 2, 3, 4]
inputDocuments: ['src/SpecScribe/GitMetrics.cs']
workflowType: 'research'
lastStep: 4
research_type: 'market'
research_topic: 'Git-activity-based code analysis tools (VCS-history-only, no static analysis) — file-level insights: authorship/ownership and co-change/change coupling'
research_goals: 'Survey the landscape of tools and techniques that derive insight purely from git activity (not static code analysis), with a focus on file-focused views: who changes a given file (authorship, ownership, knowledge distribution, bus factor) and which files commonly change alongside it (co-change / logical / change coupling). Goal is to inform improvements to SpecScribe''s own file-level git insights.'
user_name: 'Matthew-Hope Eland'
date: '2026-07-22'
web_research_enabled: true
source_verification: true
---

# Research Report: Market Research

**Date:** 2026-07-22
**Author:** Matthew-Hope Eland
**Research Type:** Market Research

---

## Research Overview

This is a **market/landscape research** effort into tools and techniques that generate
software-project insight **purely from version-control (git) activity** — commit history,
authorship, timestamps, and file-change patterns — **without** relying on static code
analysis (parsing/ASTs/type systems). The emphasis is **file-focused views**: for any given
file, *who changes it* and *what changes with it*.

---

## Research Initialization

### Research Understanding Confirmed

**Topic**: Git-activity-based code analysis tools (VCS-history-only, no static analysis) — file-level insights: authorship/ownership and co-change/change coupling
**Goals**: Survey the landscape of tools and techniques that derive insight purely from git activity, focused on file-level views (who changes a file; which files change alongside it), to inform improvements to SpecScribe's own file-level git insights.
**Research Type**: Market Research
**Date**: 2026-07-22

### Research Scope

**Focus areas (tailored to this topic):**

- **Landscape of git-history-only tools** — OSS libraries, CLIs, hosted products, and IDE/dashboard tools that analyze commit activity rather than source structure (e.g., git-of-theseus, Hercules/git-of-theseus, CodeScene, git-quick-stats, gitinspector, code-maat, Mergestat/gitql, Sourcegraph/CodeSee-style tooling).
- **File-level authorship & ownership** — who touches a file, ownership concentration, knowledge distribution, "bus factor" / truck factor, code ownership decay, recency-weighted contribution.
- **Change coupling / co-change** — files that frequently change together (logical coupling, temporal coupling, "sum of coupling"), hotspots, and how tools surface/visualize these relationships.
- **Presentation patterns** — how leading tools *render* file-focused git insight (per-file panels, coupling graphs, ownership bars, heatmaps) that SpecScribe could learn from.
- **Techniques & prior art** — the research/methodology lineage behind these features (Adam Tornhill / *Your Code as a Crime Scene*, code-maat, empirical software-engineering literature on change coupling).
- **Gaps & differentiation** — where existing tools are weak at the *single-file* view, and where SpecScribe can differentiate.

**Research Methodology:**

- Current web data with source verification (multiple independent sources for key claims)
- Confidence-level assessment for uncertain/estimated data
- Emphasis on concrete, actionable feature/presentation patterns SpecScribe can adopt

### Next Steps (Research Workflow)

1. ✅ Initialization and scope setting (current step)
2. Customer / user insights — who wants file-level git insight and why (personas, jobs-to-be-done)
3. Competitive landscape — the tools themselves, feature-by-feature, esp. file-focused views
4. Strategic synthesis — gaps, differentiation, and concrete recommendations for SpecScribe

**Research Status**: Scope confirmed by user on 2026-07-22 (feature-mining lean · deep on both priority features · inspect SpecScribe first). Research complete.

---

## Executive Summary

**The field is real, mature, and academically grounded — but the *single-file* view remains the weakest, least-standardized surface across almost every tool.** Two decades of empirical software-engineering research (evolutionary/logical coupling, code ownership, bus factor) sit behind a small set of production tools, yet most of that intelligence is presented at the *repository* or *architecture* level. The per-file "who changes this and what changes with it" question — exactly SpecScribe's weak spot — is under-served by everyone except CodeScene (paid, closed) and a handful of narrow CLIs.

**Three headline findings:**

1. **SpecScribe is already in the top tier on *shape* — the gap is *rigor of the metric*, not the presence of the feature.** SpecScribe's per-file `Contributors` and `CoupledFiles` already do what most free tools don't. The differentiation opportunity is in *how the numbers are computed*: the field has moved from raw co-change *counts* (what SpecScribe emits today) to **normalized, directional coupling strength** (a percentage / confidence), and from raw commit-counts to **ownership concentration + bus-factor** framing. Adopting those upgrades is mostly a math change on data SpecScribe already parses.

2. **The single most valuable, cheapest upgrade is directional coupling confidence.** Naive co-change ("A and B changed together 6 times") is symmetric and unnormalized, so a file that changes constantly looks coupled to everything. The research-standard fix — *confidence* = P(B changes | A changes), plus a minimum-support floor — turns "changed together 6 times" into "when you touch A, you touch B **80%** of the time." That is a dramatically more useful sentence on a file page, and SpecScribe already has every input (`ChangeCount` per file + `CoChangePairs`).

3. **The "who to ask" job-to-be-done is a genuine, repeatedly-cited pain point** (onboarding, code-review routing, troubleshooting) that `git blame` serves *badly* — and SpecScribe's per-file, non-scoreboard framing ("who do I talk to about this file?") is already aligned with where the good tools (git-who, CodeScene knowledge maps) are heading. The upgrade here is **recency-weighting and concentration** (bus factor / ownership %), not raw commit tallies.

**Bottom line for SpecScribe:** you do not need to build a new analysis engine or add git calls. The recommended moves are (a) upgrade coupling from count → directional confidence with a support floor, (b) add an ownership-concentration / bus-factor signal to the existing per-file contributor list, and (c) sharpen the file-page *presentation* of both. All three are computable from the single `--deep-git` numstat parse SpecScribe already performs.

---

## Methodology & Sources

- **Approach:** Landscape scan biased toward *actionable file-level feature and presentation patterns*, grounded first in a review of SpecScribe's own git-insights code (`GitMetrics.cs`), then external research via web search with source verification.
- **Confidence conventions:** claims about specific tool behavior are cited; where a formula is stated by a vendor without full disclosure (e.g., CodeScene's exact coupling %), it is labeled as such.
- **Deliberate boundary:** this research covers **VCS-history-only** techniques (commit graph, authorship, timestamps, co-change). Static/AST/semantic analysis is out of scope by request — noted only where a tool blends both.
- **SpecScribe baseline reviewed:** `GitMetrics.cs` — `FileInsight` (per-file `Contributors`, `CoupledFiles`, `History`), `CodeFileMetrics` (`AvgCoChanged` blast-radius, per-file contributors), `CoChangePairs` co-change map, `ClassifyCoupling` Code-vs-Process noise filter, bounded `-n 300` window, pure-SVG output.

---

## The Landscape

Git-activity-only analysis clusters into six segments. SpecScribe sits in Segment 2, borrowing techniques from Segment 1.

### Segment map

| # | Segment | What it optimizes for | Representative tools | File-level strength |
|---|---------|----------------------|---------------------|--------------------|
| 1 | **Behavioral / "crime scene" analysis** | Hotspots, change coupling, knowledge risk as *decision support* | **CodeScene** (commercial), **code-maat** (OSS, the open engine behind the book) | ⭐⭐⭐⭐⭐ (CodeScene) — the reference implementation for file-level coupling + knowledge maps |
| 2 | **Repo-report generators** (SpecScribe's neighborhood) | A readable HTML/site of git insight | **SpecScribe**, GitCommitsAnalysis, gitinspector, GitStats | ⭐⭐–⭐⭐⭐ — usually per-author/per-repo; per-file coupling rare |
| 3 | **Ownership / "who owns this" CLIs** | Fast answer to "who do I ask / who reviews this" | **git-who** (2025), git-quick-stats (`suggestReviewers`), git-fame, **truckfactor**, CODEOWNERS tooling | ⭐⭐⭐⭐ — narrow but sharp on the authorship question |
| 4 | **Longitudinal / survival analysis** | How code & authorship *age* | **git-of-theseus** (cohort/survival plots), **Hercules** (src-d; file & developer coupling matrices over time) | ⭐⭐⭐ — time-axis authorship, less per-file drill-in |
| 5 | **Research tooling & networks** | Co-editing networks, co-change clusters, impact analysis | **git2net** (co-editing networks), ModularityCheck (co-change clusters) | ⭐⭐⭐ — methodologically deep, not product-shaped |
| 6 | **Engineering-metrics / DORA platforms** | Team throughput, DevEx, delivery | GitClear, LinearB, Swarmia, Waydev | ⭐ — people/velocity focus; *not* file-level, often a SpecScribe non-goal |

### Key takeaways from the map

- **CodeScene is the benchmark to study and the benchmark to differentiate from.** It computes Hotspots, Change Coupling, and Knowledge Distribution at *file, architectural, and function* levels — the fullest file-level implementation in the market. It is commercial and closed, which is precisely SpecScribe's opening: an OSS/report-embedded tool that adopts CodeScene's *metric rigor* on the file page.
- **code-maat is the open Rosetta Stone.** Because it's the open-source engine behind *Your Code as a Crime Scene*, its analysis list is the cleanest published spec of what "git-activity file insight" should compute (details below). SpecScribe can treat code-maat's output columns as a feature checklist.
- **Segment 6 is a trap for SpecScribe.** DORA/velocity platforms rank *people*, which is an explicit SpecScribe PRD non-goal ("never author/productivity signals"). SpecScribe's per-file, non-scoreboard framing is a deliberate and defensible position — do not drift here.

---

## Deep Dive A — "Who changes this file?" (Authorship / Ownership / Bus Factor)

### What the field computes (and SpecScribe doesn't yet)

SpecScribe today lists, per file: each author → commit count touching this file + their last such commit's date, capped, with a `TotalContributors` disclosure. That is a solid **contribution list**. The field goes three steps further:

1. **Ownership *concentration*, not just a list.** code-maat's `entity-ownership` and `entity-effort` express each author's share as `author-revs / total-revs` — a *proportion*. This converts a flat list into "Alice owns 78% of this file's changes; three others share the rest." Concentration is the signal; the list is just the raw material.

2. **Main developer / knowledge owner.** code-maat's `main-dev` and the `truckfactor` tool both crown a single **knowledge owner** per file — defined as the author who edited the most lines (or made the most changes). This is the atom of the "who do I ask?" answer.

3. **Bus factor / truck factor per file.** The `truckfactor` tool computes, per file/dir/extension, *how many key developers would have to leave before knowledge is lost* — and can even **simulate a developer leaving** and show which files become "abandoned" (orphaned knowledge). A per-file bus factor of **1** is a bright-red key-person-risk flag. CodeScene productizes this exact idea as **Knowledge Distribution / key-person risk**.

### The `git blame` problem (why this matters)

The universally-cited weakness of the naive answer: **`git blame` attributes lines to whoever last touched them** — so a reformat, a file move, or a bulk lint commit reassigns "ownership" to a janitor who wrote none of the logic. Tools like **git-who** explicitly market themselves as "beyond `git blame`," analyzing *contribution patterns across whole files and directories* rather than line-level last-touch. **SpecScribe already avoids this trap** — it counts *commits touching the file per author*, not `git blame` lines — which is a correctness advantage worth stating in the UI ("contributor" = made commits touching this file, recency-aware).

### Job-to-be-done: the demand is real

The "who do I ask about this file" need shows up repeatedly as a concrete pain: **onboarding** (new hires don't know whom to ping), **code-review routing** (git-quick-stats has a literal `suggestReviewers`; auto-populating CODEOWNERS from git history is a requested GitLab feature), and **troubleshooting** (ownership tracking speeds incident response). SpecScribe's file page is a natural home for a "**Ask these people**" affordance derived from recent, concentrated contribution.

### Recommended framing for SpecScribe (preserves the non-goal)

Keep the per-file, non-scoreboard stance. Add **concentration + recency** *within* that frame:
- Show each contributor's **share %** (`author commits / file commits`), not just the count.
- Compute a lightweight **per-file bus factor** (smallest set of authors covering >50% of the file's changes) and surface `1` as a key-person-risk badge.
- **Recency-weight** the "who to ask" pick (last-90-days contribution ranks above a long-departed original author) — SpecScribe already stores `LastCommitDate` per contributor, so this is a sort/weight change, not new data.

---

## Deep Dive B — "What changes alongside this file?" (Change / Temporal / Evolutionary Coupling)

This is where SpecScribe has the **largest, cheapest upgrade available.** The terminology across the field — *temporal coupling* (Tornhill/CodeScene), *logical coupling*, *evolutionary coupling*, *co-change*, *co-committal* — all names the same phenomenon: files that repeatedly change in the same commit are probably related, even with zero static dependency between them.

### The methodology ladder (SpecScribe is on rung 1)

**Rung 1 — Raw co-change count (SpecScribe today).** "A and B changed together N times," keep pairs with N≥2, cap bulk commits (>50 files) as noise. Simple, honest, but has two known flaws: it's **unnormalized** (a file that changes constantly appears coupled to everything) and **symmetric** (can't distinguish "B always drags A along" from "A rarely involves B").

**Rung 2 — Coupling *strength* as a percentage (code-maat / CodeScene).** code-maat's `coupling` analysis outputs `degree` (a 0–100%) and `average-revs`, with thresholds `--min-shared-revs` (default 5) and `--min-coupling` (default 30%). CodeScene's **"Degree of Coupling"** is literally the headline number ("these files change together **74%** of the time"), gated by: **≥10 revisions per file** (ignore accidental coupling), **≥10 shared commits**, **≥50% strength**, and **ignore changesets >50 files**. This normalization is what makes the number trustworthy.

**Rung 3 — Directional association rules (support / confidence / lift).** The research standard treats co-change as **association-rule mining**:
- **Support** = how often the pair changes together at all (frequency / significance floor — filters coincidence).
- **Confidence** = **directional**: `confidence(A→B) = shared_changes / A_changes` = "when A changes, B changes X% of the time." Crucially **asymmetric** — `A→B ≠ B→A`. This is the single most useful upgrade: it produces the sentence *"When you edit `SiteGenerator.cs`, you edit `Charts.cs` 80% of the time — but the reverse is only 30%."*
- **Lift** = whether the co-change is more than chance given how often each file changes independently (>1 = genuinely related; ~1 = one of them just changes a lot).

### The exact upgrade for SpecScribe (data is already parsed)

SpecScribe already computes, from the one numstat parse: per-file `ChangeCount` and the full `CoChangePairs` map (shared-commit count per unordered pair). That is **exactly the three numbers** association rules need:

```
support(A,B)      = CoChangeCount(A,B)                       // already have it
confidence(A→B)   = CoChangeCount(A,B) / FileInsight[A].ChangeCount   // one division
lift(A,B)         = confidence(A→B) / ( ChangeCount[B] / totalCommits )
```

No new git call, no new parse — a computed projection over data already in `DeepGitPulse`. The per-file "Coupled files" list becomes: *"Changes with X · **confidence 80%** · lift 4.2×"*, sorted by confidence (or lift) instead of raw count, with a **min-support floor** (e.g. ≥3 shared changes) borrowed from code-maat/CodeScene to kill coincidence.

### Two high-value coupling refinements the field emphasizes

1. **"Surprising" / cross-boundary coupling is the money insight.** CodeScene's memorable framing: *"Temporal coupling is like bad weather — it gets worse with the distance you have to travel."* Two files in the *same* folder changing together is expected; a file in `src/parsing/` coupled to one in `src/rendering/` is an **architectural smell** worth surfacing loudly. SpecScribe has file paths — it can cheaply flag *cross-directory* couples as higher-signal.
2. **SpecScribe's Code-vs-Process filter is ahead of most free tools.** The `ClassifyCoupling` / `IsProcessPath` noise filter (demoting `sprint-status.yaml`-style co-commits) is a genuinely sophisticated touch that raw-count tools lack. Keep it, and consider *lift* as a second, principled noise filter (a lockfile that changes every commit will have lift ≈ 1 and self-demote).

---

## Presentation Patterns Worth Borrowing

File-level git insight lives or dies on presentation. Patterns observed across the field, mapped to SpecScribe's pure-SVG/no-JS constraint:

| Pattern | Who does it | SpecScribe fit |
|--------|-------------|----------------|
| **Coupling as a "changes with" list with a % strength** | CodeScene, code-maat | ✅ Direct: add confidence % to existing `CoupledFiles` list |
| **One-sentence natural-language insight** ("edit A → you'll likely edit B") | CodeScene | ✅ Cheap, high-impact copy change on the file page |
| **Ownership as a proportion bar** (author share of the file) | code-maat entity-effort, CodeScene knowledge map | ✅ SVG stacked bar, no JS needed |
| **Key-person-risk / bus-factor badge** | CodeScene, truckfactor | ✅ Small badge from a pure computation |
| **Cross-boundary coupling highlighted as risk** | CodeScene ("bad weather") | ✅ Path-distance heuristic, trivial with paths in hand |
| **Code age / survival ("recency within window")** | git-of-theseus, code-maat `age` | ⚠️ Partial — SpecScribe's window is bounded `-n 300`; honest as "recency," not true age |
| **Coupling network graph** | Hercules, git2net, CodeScene | ⚠️ SpecScribe has a reference graph already; risk of clutter at scale — a per-file *list* usually beats a global graph for the single-file JTBD |
| **"Suggest reviewers"** affordance | git-quick-stats | ✅ Falls out of recency-weighted ownership for free |

**Design principle from the research:** for the *single-file* question, a **ranked list with a strength number + a plain-English sentence** consistently beats a global force-directed graph. Graphs answer "show me the whole system"; the file page answers "what do I need to know about *this* file" — a list wins.

---

## Gap Analysis — SpecScribe Today vs. The Field

| Capability | Field best-practice | SpecScribe today | Gap |
|-----------|--------------------|------------------|-----|
| Per-file contributor list | ✅ (all) | ✅ `FileInsight.Contributors` | **None** — already strong, `git blame`-trap-free |
| Ownership *concentration* (share %) | ✅ code-maat, CodeScene | ❌ raw commit counts only | **Medium** — one division on existing data |
| Main dev / knowledge owner | ✅ code-maat, truckfactor | ⚠️ implicit (top of list) | **Low** — label the top-share author |
| Per-file **bus factor** / key-person risk | ✅ CodeScene, truckfactor | ❌ | **Medium** — high-value badge, cheap compute |
| Recency-weighted "who to ask" | ✅ (implied best practice) | ⚠️ has `LastCommitDate`, not weighted | **Low** — sort/weight change |
| Co-change list | ✅ (all) | ✅ `CoupledFiles` | **None** — feature present |
| Coupling **strength %** (normalized) | ✅ code-maat, CodeScene | ❌ raw counts | **HIGH-VALUE / LOW-COST** — headline upgrade |
| **Directional** confidence (A→B ≠ B→A) | ✅ research standard | ❌ symmetric | **HIGH-VALUE / LOW-COST** |
| **Lift** (chance-corrected) | ✅ research standard | ❌ | **Medium** — principled noise filter |
| Min-support / min-coupling thresholds | ✅ code-maat, CodeScene | ⚠️ only N≥2 + bulk cap | **Low** — tune floors |
| Cross-boundary coupling emphasis | ✅ CodeScene | ❌ | **Medium** — path-distance flag |
| Process/config noise filter | ⚠️ few free tools | ✅ `ClassifyCoupling` | **SpecScribe ahead** |
| True code age / survival | ✅ git-of-theseus | ⚠️ bounded-window "recency" | **Known limitation** — honesty over reach |
| People scoreboard / velocity | Segment 6 does it | ❌ (deliberate non-goal) | **Intentional non-gap** — do not build |

**Summary:** SpecScribe has the right *surfaces* and an unusually principled stance (non-scoreboard, process-noise filter, no `git blame` trap). Every meaningful gap is a **computation/presentation upgrade on already-parsed data**, not a new data-acquisition problem — a direct consequence of the "one fetch, one parse, several views" architecture already in `GitMetrics.cs`.

---

## Strategic Recommendations for SpecScribe (Prioritized)

Ranked by **value ÷ cost**. Every P0/P1 item runs on the existing `--deep-git` numstat parse — **no new git calls.**

### P0 — Directional coupling confidence + support floor (the headline win)
- On the per-file **Coupled files** list, replace/augment the raw co-change count with **confidence** `= CoChangeCount(A,B) / ChangeCount[A]` and sort by it; add a **min-support floor** (~3 shared changes) to kill coincidence.
- Render the plain-English sentence: *"When you change this file, you usually also change **X** (**80%**)."*
- **Why first:** biggest jump in usefulness, ~all inputs already in `DeepGitPulse.CoChangePairs` + `FileInsight.ChangeCount`. This is the feature users will *feel*.

### P1 — Ownership concentration + per-file bus factor
- Add **share %** to each per-file contributor (`author commits / file commits`) as a small SVG proportion bar.
- Compute a **bus factor** (min authors covering >50% of the file's changes); badge **bus factor = 1** as key-person risk.
- Add a recency-weighted **"Ask these people"** line (uses existing `LastCommitDate`).
- **Why:** turns the existing contributor list into a decision-support signal; aligns with the strongest cited JTBD (onboarding, review routing) without violating the no-scoreboard non-goal.

### P2 — Lift + cross-boundary coupling emphasis
- Add **lift** as a secondary sort/filter (self-demotes always-churning files like lockfiles; complements the existing `ClassifyCoupling` filter).
- Flag **cross-directory** couples visually as higher-signal ("surprising coupling" / architectural smell), per CodeScene's "bad weather" principle.
- **Why:** sharpens signal-to-noise; modest compute; makes SpecScribe's coupling feel *smart*, not just *present*.

### P3 — Honesty & polish
- Keep labeling window-bounded dates as **"recency within recent history"** (not true age) — a credibility asset vs. tools that overclaim; consider a config to widen `-n 300` for coupling-strength stability on mature repos (CodeScene wants ≥10 revisions before trusting a couple).
- Consider surfacing **average confidence / "blast radius"** (SpecScribe already computes `AvgCoChanged`) as a per-file "how entangled is this file?" headline.

### Explicit non-recommendations (defend the position)
- **Do not** add a people/productivity scoreboard or velocity metrics (PRD non-goal; that's Segment 6's game).
- **Do not** replace the per-file *list* with a global coupling *graph* as the primary file-page view — the research shows the list wins for the single-file question; keep the graph as the system-level view it already is.
- **Do not** adopt `git blame` line-level ownership — SpecScribe's commit-touch model is already the more robust choice; say so in the UI.

---

## Sources

**Behavioral analysis / CodeScene**
- [Architectural Analyses — CodeScene Documentation](https://docs.enterprise.codescene.io/versions/6.6.10/guides/architectural/architectural-analyses.html)
- [Behavioral Code Analysis — CodeScene](https://codescene.com/product/behavioral-code-analysis)
- [Temporal Coupling — CodeScene Documentation](https://docs.enterprise.codescene.io/versions/2.0.0/guides/technical/temporal-coupling.html)
- [Technical Debt / Hotspots — CodeScene Documentation](https://codescene.io/docs/guides/technical/hotspots.html)

**code-maat / Your Code as a Crime Scene**
- [adamtornhill/code-maat (GitHub)](https://github.com/adamtornhill/code-maat)
- [sum_of_coupling.clj — code-maat source](https://github.com/adamtornhill/code-maat/blob/master/src/code_maat/analysis/sum_of_coupling.clj)

**Ownership / bus factor / "who owns this"**
- [Git-Who: Revealing Code Ownership Beyond Git Blame — BigGo](https://biggo.com/news/202503241354_Git_Who_Tool_Reveals_Code_Ownership)
- [HelgeCPH/truckfactor (GitHub)](https://github.com/HelgeCPH/truckfactor) · [truckfactor on PyPI](https://pypi.org/project/truckfactor/0.2.5/)
- [Bus Factor In Practice (arXiv)](https://arxiv.org/pdf/2202.01523) · [Assessing the Bus Factor of Git Repositories (ResearchGate)](https://www.researchgate.net/publication/272794507_Assessing_the_Bus_Factor_of_Git_Repositories)
- [git-quick-stats](https://git-quick-stats.sh/) · [gitinspector (GitHub)](https://github.com/ejwa/gitinspector)
- [How Code Ownership Tracking Speeds Troubleshooting — GitKraken](https://www.gitkraken.com/answers/how-code-ownership-tracking-speeds-troubleshooting)
- [Autopopulate CODEOWNERS from git history — GitLab issue](https://gitlab.com/gitlab-org/gitlab/-/issues/268257)

**Longitudinal / survival / networks**
- [erikbern/git-of-theseus (GitHub)](https://github.com/erikbern/git-of-theseus) · [The half-life of code — Erik Bernhardsson](https://erikbern.com/2016/12/05/the-half-life-of-code.html)
- [src-d/hercules (GitHub)](https://github.com/src-d/hercules)
- [git2net — Mining Time-Stamped Co-Editing Networks (arXiv)](https://arxiv.org/pdf/1903.10180)

**Evolutionary coupling / co-change research**
- [Is Code Co-Committal an Indicator of Evolutionary Coupling? (MDPI Software 2026)](https://doi.org/10.3390/software5010011)
- [Detecting Evolutionary Coupling Using Transitive Association Rules (ResearchGate)](https://www.researchgate.net/publication/328910220_Research_Paper_Detecting_Evolutionary_Coupling_Using_Transitive_Association_Rules)
- [Practical Guidelines for Change Recommendation using Association Rule Mining (ResearchGate)](https://www.researchgate.net/publication/307855823_Practical_Guidelines_for_Change_Recommendation_using_Association_Rule_Mining)
- [ModularityCheck: Assessing Modularity using Co-Change Clusters (arXiv)](https://arxiv.org/pdf/1506.05754)
- [Automated Code Review Assignments / Code Ownership on GitHub (arXiv 2026)](https://arxiv.org/html/2512.05551)

---

## Appendix — code-maat analysis checklist (feature spec reference)

code-maat's `-a` analyses, treated as a "what git-activity file insight *can* compute" checklist (✅ = SpecScribe has an equivalent, ⚠️ = partial, ❌ = gap):

| code-maat analysis | Output columns | Computes | SpecScribe |
|--------------------|----------------|----------|------------|
| `revisions` | entity, n-revs | change frequency per file (min-revs 5) | ✅ hotspots / `ChangeCount` |
| `coupling` | entity, coupled, **degree %**, average-revs | logical coupling strength (min-coupling 30%, min-shared-revs 5) | ⚠️ have pairs+counts, **need degree %** |
| `soc` (sum-of-coupling) | entity, soc | total times a file is coupled to *any* other (entanglement priority) | ⚠️ `AvgCoChanged` is close |
| `authors` | entity, n-authors, n-revs | contributor breadth per file | ✅ `TotalContributors` |
| `main-dev` | entity, main-dev, ... | knowledge owner per file | ⚠️ implicit (list top) |
| `entity-ownership` | entity, author, added, deleted | per-author contribution volume | ⚠️ counts, not churn-share |
| `entity-effort` | entity, author, **author-revs, total-revs** | ownership **proportion** | ❌ **add share %** |
| `age` | entity, age-months | months since last change | ⚠️ bounded-window recency |
| `entity-churn` | entity, added, deleted | change volume | ✅ `TotalChurn` |
| `abs-churn` / `author-churn` | date/author, added, deleted | churn over time / per author | ✅ activity series / contributor churn |

<!-- Content appended by research workflow steps 2–4, 2026-07-22 -->

