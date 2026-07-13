// Vendoring script: assembles the Prism.js bundle (core + selected languages in dependency order + the SpecScribe
// line-aware driver) and a theme into src/SpecScribe/assets/{prism.js,prism.css}. NOT part of the app build — run
// by hand (`npm install && node build.js` in this folder) only when the vendored bundle needs refreshing. The
// produced prism.js / prism.css are committed; node_modules here is throwaway (gitignored).
const fs = require('fs');
const path = require('path');

const PRISM = path.join(__dirname, 'node_modules/prismjs');
const components = require(path.join(PRISM, 'components.json'));

// The languages we highlight (Prism grammar ids). Extension→id mapping lives in CodeFileTemplater.LanguageClass.
const WANT = [
  'markup', 'css', 'clike', 'javascript', 'typescript', 'jsx', 'tsx',
  'csharp', 'json', 'json5', 'yaml', 'toml', 'ini', 'bash', 'powershell',
  'python', 'sql', 'markdown', 'rust', 'go', 'java', 'kotlin', 'swift',
  'ruby', 'php', 'c', 'cpp', 'docker', 'diff', 'regex', 'graphql', 'xml-doc',
];

const langs = components.languages;

// Topological expand: pull in every require/peer dependency, then order so deps precede dependents.
function deps(id) {
  const meta = langs[id];
  if (!meta) return [];
  const d = [];
  for (const key of ['require', 'peerDependencies', 'optional']) {
    const v = meta[key];
    if (!v) continue;
    (Array.isArray(v) ? v : [v]).forEach(x => d.push(x));
  }
  return d;
}

const ordered = [];
const seen = new Set();
function visit(id) {
  if (seen.has(id) || !langs[id]) return;
  seen.add(id);
  deps(id).forEach(visit);
  ordered.push(id);
}
WANT.forEach(visit);

function read(rel) {
  return fs.readFileSync(path.join(PRISM, rel), 'utf8');
}

// Core + languages in dependency order. Manual mode: we DON'T let Prism auto-highlight (highlightAll would
// replace our <code> innerHTML and destroy the server-rendered per-line anchors). Instead a small line-aware
// driver (appended below) tokenizes the whole block for correct multi-line highlighting, then splits the
// highlighted HTML by line and injects each fragment into the matching pre-rendered `.code-line` span — so the
// locked id="L{n}" anchors and the no-JS gutter/degradation are never touched.
let bundle = '/* Prism 1.30.0 — vendored bundle (core + languages + SpecScribe line-aware driver). Do not edit by hand. */\n';
bundle += 'window.Prism=window.Prism||{};window.Prism.manual=true;\n';
bundle += read('components/prism-core.min.js') + '\n';
for (const id of ordered) {
  bundle += read(`components/prism-${id}.min.js`) + '\n';
}
bundle += fs.readFileSync(path.join(__dirname, 'driver.js'), 'utf8') + '\n';

const OUT = path.resolve(__dirname, '../../src/SpecScribe/assets');
fs.writeFileSync(path.join(OUT, 'prism.js'), bundle);

// Theme: the upstream "tomorrow" token palette; SpecScribe neutralizes its block background and tunes a couple of
// token colors for the brand surface in specscribe.css (.code-file .token.*).
const theme = read('themes/prism-tomorrow.min.css');
fs.writeFileSync(path.join(OUT, 'prism.css'), '/* Prism theme (tomorrow) — vendored. SpecScribe overrides layer on top in specscribe.css. */\n' + theme);

console.log('Languages bundled (' + ordered.length + '):', ordered.join(', '));
console.log('prism.js bytes:', bundle.length);
console.log('prism.css bytes:', theme.length);
