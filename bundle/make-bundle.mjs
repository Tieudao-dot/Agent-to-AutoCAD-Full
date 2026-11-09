#!/usr/bin/env node
/**
 * Assembles ClaudeCAD.bundle (Autodesk autoloader layout):
 *
 *   bundle/out/ClaudeCAD.bundle/
 *     PackageContents.xml
 *     Contents/
 *       ClaudeCAD.Plugin.dll         (AutoCAD 2022-2024 build, net48)
 *       2025/ClaudeCAD.Plugin.dll    (AutoCAD 2025 build, net8.0-windows)
 *       update-config.json           (only with --update-url)
 *       README.md
 *       agent/                       (dist + package.json + production node_modules for win32-x64)
 *       node/node.exe                (only with --node-dir: Node.js for Windows bundled, no separate install)
 *       skills/                      (user-editable skills)
 *       templates/templates.json     ("/" quick prompts; users add their own in %AppData%\ClaudeCAD\templates.json)
 *
 * Usage (from the repo root, after building plugin and agent):
 *   node bundle/make-bundle.mjs [--dll <path>] [--dll2025 <path>] [--out <dir>] [--skip-npm]
 *                               [--update-url <https://.../latest.json>] [--zip] [--download-url <https://.../ClaudeCAD-x.y.z.zip>]
 *                               [--node-dir <extracted node-vXX-win-x64 folder>]
 *
 * --zip also writes bundle/out/ClaudeCAD-<version>.zip and latest.json (the update
 * manifest read by CLAUDECHATUPDATE: version, url, sha256).
 *
 * Works on Windows and on Linux/macOS: node_modules are installed with
 * --os=win32 --cpu=x64 so the Windows binary of the Agent SDK is included.
 */
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..');
const { values } = parseArgs({
  options: {
    dll: { type: 'string' },
    dll2025: { type: 'string' },
    out: { type: 'string' },
    'skip-npm': { type: 'boolean' },
    'update-url': { type: 'string' },
    zip: { type: 'boolean' },
    'download-url': { type: 'string' },
    'node-dir': { type: 'string' },
  },
});

const dll = path.resolve(values.dll ?? path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'bin', 'Release', 'ClaudeCAD.Plugin.dll'));
const dll2025 = path.resolve(values.dll2025 ?? path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'bin', 'Release', 'acad2025', 'ClaudeCAD.Plugin.dll'));
const version = JSON.parse(fs.readFileSync(path.join(repo, 'agent', 'package.json'), 'utf8')).version;
const outRoot = path.resolve(values.out ?? path.join(here, 'out'));
const bundle = path.join(outRoot, 'ClaudeCAD.bundle');
const contents = path.join(bundle, 'Contents');

function fail(msg) {
  console.error(`make-bundle: ${msg}`);
  process.exit(1);
}

if (!fs.existsSync(dll)) fail(`plugin dll not found: ${dll}\n  build it first: dotnet build plugin/ClaudeCAD.Plugin/ClaudeCAD.Plugin.csproj -c Release`);
const agentDist = path.join(repo, 'agent', 'dist', 'index.js');
if (!fs.existsSync(agentDist)) fail('agent/dist/index.js not found; run "npm run build" in agent/ first');

fs.rmSync(bundle, { recursive: true, force: true });
fs.mkdirSync(contents, { recursive: true });

// manifest with the release version stamped in
const manifest = fs
  .readFileSync(path.join(here, 'PackageContents.xml'), 'utf8')
  .replace(/AppVersion="[^"]*"/, `AppVersion="${version}"`)
  .replace(/(<ComponentEntry[\s\S]*?)Version="[^"]*"/g, `$1Version="${version}"`);
fs.writeFileSync(path.join(bundle, 'PackageContents.xml'), manifest, 'utf8');

function copyDll(src, destDir) {
  fs.mkdirSync(destDir, { recursive: true });
  fs.copyFileSync(src, path.join(destDir, 'ClaudeCAD.Plugin.dll'));
  const pdb = src.replace(/\.dll$/i, '.pdb');
  if (fs.existsSync(pdb)) fs.copyFileSync(pdb, path.join(destDir, 'ClaudeCAD.Plugin.pdb'));
}
copyDll(dll, contents);
if (fs.existsSync(dll2025)) copyDll(dll2025, path.join(contents, '2025'));
else console.warn(`make-bundle: AutoCAD 2025 build not found (${dll2025}); the bundle will only load in AutoCAD 2022-2024.`);
if (values['update-url']) {
  fs.writeFileSync(path.join(contents, 'update-config.json'), JSON.stringify({ manifest_url: values['update-url'] }, null, 2), 'utf8');
}
fs.copyFileSync(path.join(repo, 'README.md'), path.join(contents, 'README.md'));

// skills (only folders with SKILL.md + the README) and the "/" quick prompts
fs.cpSync(path.join(repo, 'skills'), path.join(contents, 'skills'), { recursive: true });
fs.cpSync(path.join(repo, 'templates'), path.join(contents, 'templates'), { recursive: true });

// agent: compiled JS + manifest + production dependencies
const agentOut = path.join(contents, 'agent');
fs.mkdirSync(agentOut, { recursive: true });
fs.cpSync(path.join(repo, 'agent', 'dist'), path.join(agentOut, 'dist'), { recursive: true });
for (const f of ['package.json', 'package-lock.json']) fs.copyFileSync(path.join(repo, 'agent', f), path.join(agentOut, f));

if (!values['skip-npm']) {
  const npm = process.platform === 'win32' ? 'npm.cmd' : 'npm';
  console.log('make-bundle: installing production dependencies for win32-x64 ...');
  execFileSync(npm, ['ci', '--omit=dev', '--os=win32', '--cpu=x64', '--no-audit', '--no-fund'], {
    cwd: agentOut,
    stdio: 'inherit',
    shell: process.platform === 'win32',
  });
  const winBin = path.join(agentOut, 'node_modules', '@anthropic-ai', 'claude-agent-sdk-win32-x64');
  if (!fs.existsSync(winBin)) fail('the win32-x64 Claude Code binary package was not installed (npm too old? needs npm >= 9)');
  if (!fs.existsSync(path.join(winBin, 'claude.exe'))) fail('claude.exe missing in the win32-x64 package (the installer and "Đăng nhập Claude" run it)');
}

// Source maps (*.map, ~2,200 files) are only for debugging the libraries: not shipped. Fewer files = faster
// install, less antivirus scanning, smaller setup.
{
  let removed = 0;
  const walk = (dir) => {
    if (!fs.existsSync(dir)) return;
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) walk(p);
      else if (e.name.endsWith('.map')) { fs.rmSync(p); removed++; }
    }
  };
  walk(agentOut);
  if (removed) console.log(`make-bundle: removed ${removed} source maps`);
}

// Node.js for Windows next to the plugin (Contents\node\node.exe): the user does not install Node
if (values['node-dir']) {
  const nodeDir = path.resolve(values['node-dir']);
  const exe = path.join(nodeDir, 'node.exe');
  if (!fs.existsSync(exe)) fail(`node.exe not found in ${nodeDir}`);
  const nodeOut = path.join(contents, 'node');
  fs.mkdirSync(nodeOut, { recursive: true });
  fs.copyFileSync(exe, path.join(nodeOut, 'node.exe'));
  if (fs.existsSync(path.join(nodeDir, 'LICENSE'))) fs.copyFileSync(path.join(nodeDir, 'LICENSE'), path.join(nodeOut, 'LICENSE'));
  console.log(`make-bundle: bundled ${exe}`);
}

if (values.zip) {
  const zipName = `ClaudeCAD-${version}.zip`;
  const zipPath = path.join(outRoot, zipName);
  fs.rmSync(zipPath, { force: true });
  if (process.platform === 'win32') {
    execFileSync('powershell', ['-NoProfile', '-Command', `Compress-Archive -Path '${bundle}' -DestinationPath '${zipPath}'`], { stdio: 'inherit' });
  } else {
    execFileSync('zip', ['-qr', zipPath, 'ClaudeCAD.bundle'], { cwd: outRoot, stdio: 'inherit' });
  }
  const { createHash } = await import('node:crypto');
  const sha256 = createHash('sha256').update(fs.readFileSync(zipPath)).digest('hex');
  const url = values['download-url'] ?? zipName;
  fs.writeFileSync(path.join(outRoot, 'latest.json'), JSON.stringify({ version, url, sha256, notes: '' }, null, 2), 'utf8');
  console.log(`make-bundle: ${zipPath} (sha256 ${sha256}) + latest.json`);
}

console.log(`make-bundle: done → ${bundle} (version ${version})`);
console.log('Install: copy ClaudeCAD.bundle to %AppData%\\Autodesk\\ApplicationPlugins and restart AutoCAD (2022-2025).');
