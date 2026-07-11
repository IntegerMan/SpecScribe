#!/usr/bin/env node
// Story 6.6 DELIVERY-ARCHITECTURE SPIKE — throwaway npm-wrapper-around-native-binary launcher.
//
// Proves the axis-D "distribute the C# tool via npx, no .NET SDK required" path (the esbuild/Biome pattern): an
// `npx <pkg>` invocation lands in this launcher, which resolves the platform's self-contained `specscribe`
// binary and spawns it, forwarding argv + exit code. The native binary is a `dotnet publish -r <rid>
// --self-contained -p:PublishSingleFile=true` artifact (~73 MB raw / ~34 MB gzipped per RID) — it carries its
// own .NET runtime, so the end user needs neither the .NET SDK nor the runtime installed.
//
// Real distribution would resolve the binary from a per-RID optionalDependency package (@specscribe/win-x64,
// …) so only the current platform's binary is downloaded. For the SPIKE we resolve it from:
//   1. $SPECSCRIBE_BIN (explicit override — used to prove the chain locally), else
//   2. a conventional per-platform path next to the wrapper (documented, not populated in the spike).
const { spawnSync } = require('node:child_process');
const path = require('node:path');
const fs = require('node:fs');

function resolveBinary() {
  if (process.env.SPECSCRIBE_BIN && fs.existsSync(process.env.SPECSCRIBE_BIN)) {
    return process.env.SPECSCRIBE_BIN;
  }
  // The real optionalDependency layout: @specscribe/<rid>/specscribe(.exe). Mapped from Node's platform/arch.
  const rid = ({
    'win32 x64': 'win-x64', 'win32 arm64': 'win-arm64',
    'linux x64': 'linux-x64', 'linux arm64': 'linux-arm64',
    'darwin x64': 'osx-x64', 'darwin arm64': 'osx-arm64',
  })[`${process.platform} ${process.arch}`];
  const exe = process.platform === 'win32' ? 'specscribe.exe' : 'specscribe';
  const candidate = path.join(__dirname, '..', 'native', rid || 'unknown', exe);
  if (fs.existsSync(candidate)) return candidate;
  console.error(
    `[specscribe-spike] no native binary for ${process.platform}/${process.arch}.\n` +
    `  Set SPECSCRIBE_BIN to a self-contained specscribe build, or (real dist) install @specscribe/${rid}.`);
  process.exit(127);
}

const bin = resolveBinary();
const res = spawnSync(bin, process.argv.slice(2), { stdio: 'inherit' });
process.exit(res.status === null ? 1 : res.status);
