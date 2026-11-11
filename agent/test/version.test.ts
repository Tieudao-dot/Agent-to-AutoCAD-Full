import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const read = (p: string) => fs.readFileSync(path.join(repo, p), 'utf8');

describe('release version', () => {
  it('is the same in agent/package.json, the sidecar, the plugin and the bundle manifest', () => {
    const v = JSON.parse(read('agent/package.json')).version as string;
    assert.match(read('agent/src/index.ts'), new RegExp(`VERSION = '${v.replace(/\./g, '\\.')}'`));
    assert.match(read('plugin/ClaudeCAD.Plugin/ClaudeCAD.Plugin.csproj'), new RegExp(`<Version>${v.replace(/\./g, '\\.')}</Version>`));
    assert.match(read('bundle/PackageContents.xml'), new RegExp(`AppVersion="${v.replace(/\./g, '\\.')}"`));
  });

  it('bundle manifest covers AutoCAD 2022-2027 with the right dll per series', () => {
    const xml = read('bundle/PackageContents.xml');
    assert.match(xml, /<RuntimeRequirements OS="Win64" Platform="AutoCAD\*" SeriesMin="R24\.1" SeriesMax="R26\.0" \/>\s*<Components/);
    assert.match(xml, /SeriesMin="R24\.1" SeriesMax="R24\.3"[\s\S]*?ModuleName="\.\/Contents\/ClaudeCAD\.Plugin\.dll"/);
    // 2026 (R25.1) shares the .NET 8 build with 2025; 2027 (R26.0, .NET 10) is not binary compatible and has its own
    assert.match(xml, /SeriesMin="R25\.0" SeriesMax="R25\.1"[\s\S]*?ModuleName="\.\/Contents\/2025\/ClaudeCAD\.Plugin\.dll"/);
    assert.match(xml, /SeriesMin="R26\.0" SeriesMax="R26\.0"[\s\S]*?ModuleName="\.\/Contents\/2027\/ClaudeCAD\.Plugin\.dll"/);
    // every component names an explicit last release: a future AutoCAD must not load an untested build
    for (const m of xml.matchAll(/SeriesMax="([^"]*)"/g)) assert.match(m[1], /^R\d+\.\d+$/);
  });
});
