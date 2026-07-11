// esbuild config for the SpecScribe Status extension (the current recommended VS Code extension bundler —
// toolchain confirmed by the Story 6.3 spike). Bundles src/extension.ts to dist/extension.js.
const esbuild = require('esbuild');

const production = process.argv.includes('--production');
const watch = process.argv.includes('--watch');

async function main() {
  const ctx = await esbuild.context({
    entryPoints: ['src/extension.ts'],
    bundle: true,
    format: 'cjs',
    platform: 'node',
    target: 'node18',            // VS Code's extension host runtime floor
    outfile: 'dist/extension.js',
    external: ['vscode'],        // provided by the host at runtime; never bundle it
    sourcemap: !production,
    minify: production,
    logLevel: 'info',
  });
  if (watch) {
    await ctx.watch();
    console.log('[esbuild] watching…');
  } else {
    await ctx.rebuild();
    await ctx.dispose();
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
