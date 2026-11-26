/**
 * Sửa CI đợt H (26/11/2026, docs/ban-giao/nang-cap-h-sua-ci.md): a pwsh step of the Windows CI fails only on the exit
 * code of its last native command, so every native command of a multi-command step must check its own exit code
 * (a failing `npm test` followed by a passing `npm run build` left the step green).
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const yml = fs.readFileSync(path.join(repo, '.github', 'workflows', 'build.yml'), 'utf8').replace(/\r\n/g, '\n');
const check = 'if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }';

/** The lines of the `run: |` block of the step with this name. */
function runLines(step: string): string[] {
  const i = yml.indexOf(`- name: ${step}`);
  assert.ok(i >= 0, step);
  const lines = yml.slice(i).split('\n');
  const r = lines.findIndex((l) => /^\s+run: \|$/.test(l));
  const indent = lines[r + 1]!.match(/^\s*/)![0].length;
  const out: string[] = [];
  for (const l of lines.slice(r + 1)) {
    if (l.trim() && l.match(/^\s*/)![0].length < indent) break;
    out.push(l.trim());
  }
  return out.filter((l) => l);
}

describe('Sửa CI đợt H: multi-command steps stop on the first failing command', () => {
  it('npm ci, npm test and npm run build each check their exit code', () => {
    const l = runLines('Sidecar - install, test (IPC over Windows named pipes), build');
    for (const cmd of ['npm ci', 'npm test', 'npm run build']) {
      const k = l.indexOf(cmd);
      assert.ok(k >= 0, cmd);
      assert.equal(l[k + 1], check, `${cmd} is followed by the exit code check`);
    }
  });

  it('choco and ISCC each check their exit code before the installer is copied', () => {
    const l = runLines('Setup.exe (Inno Setup)');
    const choco = l.findIndex((x) => x.startsWith('choco install'));
    const iscc = l.findIndex((x) => x.includes('ISCC.exe'));
    assert.equal(l[choco + 1], check);
    assert.equal(l[iscc + 1], check);
  });
});
