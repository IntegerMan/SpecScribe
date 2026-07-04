# DocsForge

A .NET 10 console tool that watches `_bmad-output/**/*.md` and generates a stylized,
navigable HTML site into `docs/live/` — regenerated automatically on every save.

## Run

From the repo root, double-click **`watch-docs.bat`**, or:

```
watch-docs.bat            # build once, then watch for changes (Ctrl+C to stop)
watch-docs.bat --once     # single generation pass, no watching
```

Output is written to `docs/live/index.html` and mirrors the `_bmad-output/`
folder structure. Source files are always read with shared access — the
watcher never holds a write lock on anything in `_bmad-output/`.

The generated site's styling lives in `assets/docsforge.css` and reuses the
warm/parchment palette from the hand-crafted `docs/*.html` pages, but is
generic enough to render any BMad markdown file well without per-document
tuning.

The site is branded with the game's name, read from `project_name` in
`_bmad/config.toml` (falls back to "BMad Live Docs" if absent). Status colors
follow one rule everywhere: parchment = pending, gold = drafted/ready,
teal = in development, green = done only.
