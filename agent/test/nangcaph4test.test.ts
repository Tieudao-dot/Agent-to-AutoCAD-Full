/**
 * Test & sửa đợt H4 (26/11/2026, docs/ban-giao/nang-cap-h4-test.md): company_standard between the assistant and the plugin.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { z } from 'zod';
import { buildTools } from '../src/tools/index.js';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const read = (...p: string[]) => fs.readFileSync(path.join(repo, ...p), 'utf8');
const tools = new Map(buildTools({ call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} }).map((t) => [t.name, t]));
const schemaOf = (n: string) => z.object(tools.get(n)!.inputSchema as z.ZodRawShape);

describe('Test & sửa H4', () => {
  it('every parameter company_standard reads is in its schema', () => {
    const src = read('plugin', 'ClaudeCAD.Plugin', 'Drawing', 'CompanyStandardTools.cs');
    const body = src.slice(src.indexOf('internal static object CompanyStandardTool('), src.indexOf('/// <summary>Layers, styles, layouts'));
    const reads = new Set([...body.matchAll(/p\.(?:Str|Num|Bool|Has|Get)\("([a-z_]+)"/g)].map((m) => m[1]!));
    const keys = new Set(Object.keys(schemaOf('company_standard').shape));
    for (const k of reads) assert.ok(keys.has(k), `plugin reads '${k}'`);
    assert.ok(reads.has('file') && reads.has('name') && reads.has('action'));
  });

  it('the calls of the checklist parse; a name too long or a wrong action does not', () => {
    const cs = schemaOf('company_standard');
    assert.ok(cs.safeParse({ action: 'learn', file: 'D:\\Mau\\ABC.dwt', name: 'Công ty ABC' }).success);
    assert.ok(cs.safeParse({ action: 'delete', name: 'ABC', confirm_handles: ['0123456789ABCDEF'] }).success);
    assert.ok(!cs.safeParse({ action: 'apply', name: 'ABC' }).success);
    assert.ok(!cs.safeParse({ action: 'learn', name: 'x'.repeat(61) }).success);
    assert.ok(schemaOf('fix_drawing').safeParse({ fix: 'nonstandard_styles', company: 'ABC' }).success);
    assert.ok(schemaOf('setup_standards').safeParse({ company: 'ABC' }).success, 'no scale needed with company');
  });

  it('the plugin refuses a standard group of fix_drawing after a company check, before it scans', () => {
    const ck = read('plugin', 'ClaudeCAD.Plugin', 'Drawing', 'DrawingCheckTools.cs');
    assert.match(ck, /if \(p\.Has\("company"\) && def\.NeedsStandard\)/);
    assert.match(ck, /fix_drawing does not apply a company standard/);
  });
});
