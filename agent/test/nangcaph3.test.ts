/**
 * Nâng cấp đợt H3 (26/11/2026, docs/ban-giao/nang-cap-h3.md): compare_drawings between the assistant and the plugin.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { z } from 'zod';
import { F5_CARD_TOOLS } from '../src/agent/confirmation.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import { ALWAYS_LOADED_TOOLS, buildTools } from '../src/tools/index.js';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const read = (...p: string[]) => fs.readFileSync(path.join(repo, ...p), 'utf8');
const tool = buildTools({ call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} }).find((t) => t.name === 'compare_drawings')!;
const schema = z.object(tool.inputSchema as z.ZodRawShape);

describe('H3: compare_drawings', () => {
  it('is deferred, asks the card for many clouds and is named in the prompt of every plan', () => {
    assert.ok(tool);
    assert.ok(!ALWAYS_LOADED_TOOLS.includes('compare_drawings'));
    assert.ok(F5_CARD_TOOLS.has('compare_drawings'));
    for (const plan of ['2d', '3d', '2d3d'] as const) assert.match(systemPromptFor(plan), /compare_drawings/, plan);
  });

  it('every parameter the plugin reads is in the schema', () => {
    const src = read('plugin', 'ClaudeCAD.Plugin', 'Drawing', 'CompareTools.cs');
    const body = src.slice(src.indexOf('internal static object CompareDrawings('), src.indexOf('private static CmpItem ItemOf('));
    const reads = new Set([...body.matchAll(/p\.(?:Str|Num|Bool|Has|Get)\("([a-z_]+)"/g)].map((m) => m[1]!));
    assert.ok(reads.size >= 5, [...reads].join(','));
    const keys = new Set(Object.keys(schema.shape));
    for (const k of reads) assert.ok(keys.has(k), `plugin reads '${k}' that the schema does not offer`);
    for (const k of ['excel', 'confirm_handles']) assert.ok(keys.has(k), k);
  });

  it('the schema takes the calls of the checklist and refuses wrong ones', () => {
    assert.ok(schema.safeParse({ file: 'D:\\Du an\\Nha pho - ban cu.dwg' }).success);
    assert.ok(schema.safeParse({ file: 'C:/a/b.dxf', other_is: 'new', match: 'geometry', tolerance: 5, mark: true, excel: true }).success);
    assert.ok(!schema.safeParse({}).success, 'the file is needed');
    assert.ok(!schema.safeParse({ file: 'a.dwg', match: 'fuzzy' }).success);
    assert.ok(!schema.safeParse({ file: 'a.dwg', tolerance: 0 }).success);
    assert.ok(!schema.safeParse({ file: 'a.dwg', other_is: 'older' }).success);
  });

  it('the description stays short (deferred, loaded only when needed)', () => {
    const size = tool.description.length + JSON.stringify(z.toJSONSchema(schema)).length;
    assert.ok(size < 2500, String(size));
  });
});
