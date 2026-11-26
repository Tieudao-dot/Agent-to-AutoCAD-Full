/**
 * Nâng cấp đợt H7 (docs/ban-giao/nang-cap-h7.md): draw_mep / mep_report on the assistant side: deferred, 3D / Full only,
 * the schema, the card of the Excel file, one prompt line, the approved standard numbers (26/11/2026) in the descriptions.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { z } from 'zod';
import { F5_CARD_TOOLS } from '../src/agent/confirmation.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import { MEP_TOOL_NAMES } from '../src/tools/mep.js';
import { ALWAYS_LOADED_TOOLS, buildTools } from '../src/tools/index.js';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
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

  it('one prompt line on 3D / Full, none on 2D; the descriptions give the approved numbers of the plugin', () => {
    for (const p of ['3d', '2d3d'] as const) assert.match(systemPromptFor(p), /draw_mep/);
    assert.ok(!systemPromptFor('2d').includes('draw_mep'));
    const text = MEP_TOOL_NAMES.map((n) => (tools.find((t) => t.name === n) as any).description).join(' ');
    assert.match(text, /never sizes pipes/);
    // the numbers approved 26/11/2026 are the plugin's (H7Numbers, Core/Mep.cs)
    const cs = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'Mep.cs'), 'utf8');
    const num = (name: string) => cs.match(new RegExp(`${name} = ([0-9.]+)`))![1]!;
    for (const [k, label] of [['SupplyBranchMm', 'branch D'], ['SupplyMainMm', 'main D'], ['DrainFloorMm', 'floor D'], ['DrainWcMm', 'stack D'], ['VentMm', 'vent D']] as const)
      assert.ok(text.includes(label + num(k)), `${label}${num(k)}`);
    assert.ok(text.includes(`falls ${num('DrainSlopePercent')} %`));
    assert.ok(text.includes(`elbows ${num('PipeBendFactor')} × D`));
    const schema = JSON.stringify(Object.fromEntries(Object.entries((tools.find((t) => t.name === 'draw_mep') as any).inputSchema).map(([k, v]) => [k, (v as any).description])));
    assert.ok(schema.includes(`${num('TopBelowBeamMm')} mm under the lowest beam`));
    assert.ok(schema.includes(`${num('TopBelowSlabMm')} mm under the slab`));
    const rs = JSON.stringify(Object.fromEntries(Object.entries((tools.find((t) => t.name === 'mep_report') as any).inputSchema).map(([k, v]) => [k, (v as any).description])));
    assert.ok(rs.includes(`(default ${num('ClearanceMm')})`));
  });
});
