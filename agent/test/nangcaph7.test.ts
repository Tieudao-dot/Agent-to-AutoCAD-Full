/**
 * Nâng cấp đợt H7 (docs/ban-giao/nang-cap-h7.md): draw_mep / mep_report on the assistant side: deferred, 3D / Full only,
 * the schema, the card of the Excel file, one prompt line (no standard number in the descriptions until approved).
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { z } from 'zod';
import { F5_CARD_TOOLS } from '../src/agent/confirmation.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import { MEP_TOOL_NAMES } from '../src/tools/mep.js';
import { ALWAYS_LOADED_TOOLS, buildTools } from '../src/tools/index.js';

const bridge = { call: async () => ({ id: 'x', ok: true as const, result: {} }), status: () => {} };
const tools = buildTools(bridge);
const schemaOf = (n: string) => z.object((tools.find((t) => t.name === n) as any).inputSchema as z.ZodRawShape);

describe('H7: MEP pipes in 3D', () => {
  it('two deferred tools; the Excel file of the report has the card', () => {
    for (const n of MEP_TOOL_NAMES) {
      assert.ok(tools.some((t) => t.name === n), n);
      assert.ok(!ALWAYS_LOADED_TOOLS.includes(n), `${n} is deferred`);
    }
    assert.ok(F5_CARD_TOOLS.has('mep_report'));
  });

  it('the schema takes a route in plan or in 3D and refuses an unknown system', () => {
    const d = schemaOf('draw_mep');
    assert.ok(d.safeParse({ system: 'drain', use: 'wc', points: [[0, 0], [3000, 0], [3000, 2000]], elevation: 2800 }).success);
    assert.ok(d.safeParse({ system: 'duct_supply', width: 400, height: 300, points: [[0, 0, 3000], [5000, 0, 3000]] }).success);
    assert.ok(d.safeParse({ system: 'supply', diameter: 25, points: [[0, 0], [1000, 0]], under_beams: true, connect_to: '2A1F' }).success);
    assert.ok(!d.safeParse({ system: 'gas', points: [[0, 0], [1, 0]] }).success);
    assert.ok(!d.safeParse({ system: 'supply', points: [[0, 0]] }).success, 'at least two points');
    assert.ok(!d.safeParse({ system: 'drain', points: [[0, 0], [1, 0]], slope: -1 }).success);
    const r = schemaOf('mep_report');
    assert.ok(r.safeParse({ action: 'schedule', waste_percent: 5, excel: true }).success);
    assert.ok(r.safeParse({ action: 'clash', clearance: 30, other_handles: ['1A'] }).success);
    assert.ok(!r.safeParse({ action: 'erase' }).success);
  });

  it('one prompt line on 3D / Full, none on 2D; no standard default number in the descriptions', () => {
    for (const p of ['3d', '2d3d'] as const) assert.match(systemPromptFor(p), /draw_mep/);
    assert.ok(!systemPromptFor('2d').includes('draw_mep'));
    const text = MEP_TOOL_NAMES.map((n) => JSON.stringify((tools.find((t) => t.name === n) as any).description)).join(' ');
    for (const n of ['D25', 'D32', 'D60', 'D110', '2%', '50 mm', '100 mm']) assert.ok(!text.includes(n), n);
    assert.match(text, /never sizes pipes/);
  });
});
