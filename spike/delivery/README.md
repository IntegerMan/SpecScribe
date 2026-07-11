# `spike/delivery` — Story 6.6 Delivery-Architecture & Distribution Spike

Everything here is **throwaway** and quarantined (see [`spike/README.md`](../README.md)): no `.sln` references it,
it is not part of `src/SpecScribe`'s build or `dotnet pack`, and it contributes **no** code path to the shipped
`specscribe` tool. The generated site is byte-identical with or without this folder. **The durable output is
[ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md)** — the code here is the evidence behind
it and can be deleted once the ADR is ratified.

It measures the four separable axes of the "JSON + SPA + npx vs. C# static-site + bundled-binary" decision:

- `exporter/` — a C# console app (`specscribe-delivery-spike`) that reuses the **exact** ingest + view-model path
  the HTML surface uses (`BmadArtifactAdapter.Ingest` → `DashboardViewBuilder` / `EpicsViewBuilder` →
  `HtmlRenderAdapter`). It emits a **JSON data layer** (`data.json` — the structured Story 6.2 section records) +
  the pre-rendered section bodies (`bodies.json`), and prints an **axis-A bloat report** (the 3-way byte split:
  pre-rendered body vs. structured data vs. inline-SVG chart mass). No `.md` re-parse, no `.html` scrape (AD-1/AD-2).
- `spa/` — a ~90-line **vanilla-JS SPA** (`index.html` + `app.js`) that renders the dashboard + epics from the JSON
  with in-view navigation and reports client render latency. It renders the non-chart sections itself from
  `data.json` and injects the pre-rendered chart panels from `bodies.json`.
- `npm-wrapper/` — the **npm-wrapper-around-native-binary** npx proof (the esbuild/Biome pattern): a ~1.5 KB wrapper
  whose `bin` resolves + spawns the self-contained `specscribe` binary, so `npx <pkg>` runs it with **no .NET SDK**.

## Reproduce the measurements

```bash
# Axis A — JSON data layer + bloat report (writes data.json + bodies.json, prints the byte split to stderr):
dotnet run --project spike/delivery/exporter -c Release -- "." --out <outdir>

# Axis A — the SPA (serve <outdir> alongside spa/index.html + app.js and open it):
#   copy data.json + bodies.json next to spa/index.html, then `python -m http.server` and load index.html.

# Axis D — the self-contained binary + npx wrapper:
dotnet publish src/SpecScribe/SpecScribe.csproj -c Release -r <rid> --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o <pubdir>
cd spike/delivery/npm-wrapper && npm pack            # ~1.5 KB wrapper tarball
SPECSCRIBE_BIN=<pubdir>/specscribe(.exe) node bin/specscribe.js generate --output <site>   # wrapper → native exe
```

## Findings (measured against this repo, 2026-07-10) — full analysis in ADR 0006

| Axis | Number |
|---|---|
| Static site today | 198 files (196 HTML), 5.88 MB; cold/warm generate ~3.2 s |
| Chart mass | inline SVG = 69.3 % of the dashboard body, 58.9 % of epics; 2,469 SVGs / 1.37 MB site-wide |
| Structured (non-chart) data layer, both surfaces | 13.5 KB |
| Client render | fetch 35 ms, render 7.9 ms (dashboard) / 6.9 ms (epics) |
| Epic-7 file-count extrapolation (this small repo) | +863 pages → ~1,060 files; large repos → thousands |
| Self-contained binary | 73.0 MiB raw / 34.2 MB gzipped per RID |
| npx wrapper | 1.5 KB; `npx`→wrapper→native exe generated 198 files (196 HTML) in 3.7 s, no .NET SDK |
| Port surface (axis C, not performed) | ~14,200 LOC / 87 files + ~676 tests; Markdig + deep-git are the risk clusters |

**Verdict:** each concern (file bloat, npx, thin extension) is addressable **without** the C#→TS port; the coupling
that would force the port holds only for the full simultaneous set. See ADR 0006 for the decision.
