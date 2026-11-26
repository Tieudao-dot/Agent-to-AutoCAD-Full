/**
 * Nâng cấp đợt H8 (docs/ban-giao/nang-cap-h8.md): build_footing (3D / Full), draw_rebar and rebar_schedule (2D / Full) on the
 * assistant side: deferred, the schema (main bars from the user), the card of the Excel file, the prompt lines, the approved
 * numbers of the plugin in the descriptions.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { z } from 'zod';
import { F5_CARD_TOOLS } from '../src/agent/confirmation.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import { FOOTING_TOOL_NAMES, REBAR_TOOL_NAMES } from '../src/tools/structure.js';
import { ALWAYS_LOADED_TOOLS, buildTools } from '../src/tools/index.js';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const bridge = { call: async () => ({ id: 'x', ok: true as const, result: {} }), status: () => {} };
const tools = buildTools(bridge);
const tool = (n: string) => tools.find((t) => t.name === n) as any;
const schemaOf = (n: string) => z.object(tool(n).inputSchema as z.ZodRawShape);

describe('H8: structure', () => {
  it('three deferred tools; the Excel file of the schedule has the card', () => {
    for (const n of [...FOOTING_TOOL_NAMES, ...REBAR_TOOL_NAMES]) {
      assert.ok(tool(n), n);
      assert.ok(!ALWAYS_LOADED_TOOLS.includes(n), `${n} is deferred`);
    }
    assert.ok(F5_CARD_TOOLS.has('rebar_schedule'));
  });

  it('the main bars are written as the user says them; a wrong form is refused', () => {
    const d = schemaOf('draw_rebar');
    assert.ok(d.safeParse({ element: 'beam', position: [0, 0], width: 220, height: 400, span: 5000, bottom: '2Ø18', top: '2Ø16' }).success);
    assert.ok(d.safeParse({ element: 'column', position: [0, 0], width: 300, height: 300, length: 3300, bars: '8D20', tie: 'Ø6a200' }).success);
    assert.ok(d.safeParse({ element: 'slab', position: [0, 0], width: 4000, length: 3000, bars_x: 'Ø8a200' }).success);
    assert.ok(!d.safeParse({ element: 'beam', position: [0, 0], width: 220, bottom: 'two bars' }).success);
    assert.ok(!d.safeParse({ element: 'wall', position: [0, 0], width: 220 }).success);
    const f = schemaOf('build_footing');
    assert.ok(f.safeParse({ kind: 'pad', positions: [[0, 0], [3000, 0]], size: [1500, 1500] }).success);
    assert.ok(f.safeParse({ kind: 'strip', path_points: [[0, 0], [6000, 0]], rib: false }).success);
    assert.ok(schemaOf('rebar_schedule').safeParse({ table_position: [0, 0], excel: true, waste_percent: 3 }).success);
  });

  it('the prompt names the tools on their plans; the descriptions give the approved numbers of the plugin', () => {
    assert.match(systemPromptFor('2d'), /draw_rebar/);
    assert.ok(!systemPromptFor('3d').includes('draw_rebar'));
    assert.match(systemPromptFor('3d'), /build_footing/);
    assert.ok(!systemPromptFor('2d').includes('build_footing'));
    const cs = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'Rebar.cs'), 'utf8');
    const num = (name: string) => cs.match(new RegExp(`${name} = ([0-9.]+)`))![1]!;
    const text = [...FOOTING_TOOL_NAMES, ...REBAR_TOOL_NAMES].map((n) => tool(n).description).join(' ');
    assert.match(text, /never choose them/);
    assert.ok(text.includes(`slab ${num('CoverSlab')}, beam ${num('CoverBeam')}, column ${num('CoverColumn')} mm`));
    assert.ok(text.includes(`anchorage ${num('AnchorFactor')}d, lap ${num('LapBeamSlabFactor')}d (column ${num('LapColumnFactor')}d)`));
    assert.ok(text.includes(`${num('KgPerMFactor')} × d²`));
    assert.ok(text.includes(`pad ${num('PadSize')} × ${num('PadSize')}, base ${num('PadHeight')}`));
    assert.ok(text.includes('cỡ mẫu, kỹ sư quyết'));
    assert.match(text, /the engineer checks them against the TCVN in force/);
  });
});
