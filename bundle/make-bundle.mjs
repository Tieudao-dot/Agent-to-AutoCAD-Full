#!/usr/bin/env node
/**
 * Assembles ClaudeCAD.bundle (Autodesk autoloader layout):
 *
 *   bundle/out/ClaudeCAD.bundle/
 *     PackageContents.xml
 *     Contents/
 *       ClaudeCAD.Plugin.dll         (built plugin)
 *       README.md
 *       agent/                       (dist + package.json + production node_modules for win32-x64)
 *       skills/                      (user-editable skills)
 *
 * Usage (from the repo root, after building plugin and agent):
 *   node bundle/make-bundle.mjs [--dll <path>] [--out <dir>] [--skip-npm]
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
    out: { type: 'string' },
    'skip-npm': { type: 'boolean' },
  },
});

const dll = path.resolve(values.dll ?? path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'bin', 'Release', 'ClaudeCAD.Plugin.dll'));
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

fs.copyFileSync(path.join(here, 'PackageContents.xml'), path.join(bundle, 'PackageContents.xml'));
fs.copyFileSync(dll, path.join(contents, 'ClaudeCAD.Plugin.dll'));
const pdb = dll.replace(/\.dll$/i, '.pdb');
if (fs.existsSync(pdb)) fs.copyFileSync(pdb, path.join(contents, 'ClaudeCAD.Plugin.pdb'));
fs.copyFileSync(path.join(repo, 'README.md'), path.join(contents, 'README.md'));

// skills (only folders with SKILL.md + the README)
fs.cpSync(path.join(repo, 'skills'), path.join(contents, 'skills'), { recursive: true });

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
}

console.log(`make-bundle: done → ${bundle}`);
console.log('Install: copy ClaudeCAD.bundle to %AppData%\\Autodesk\\ApplicationPlugins and restart AutoCAD 2022.');
