// Sau kiểm tra độc lập đợt F + G (docs/ban-giao/kiem-tra-doc-lap-dot-fg.md): KFG-25 (quantities by plan), KFG-19 (empty
// lists refused by the schema), KFG-14 (plan scope of đợt G, pictures are data).
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const tool = (name: string, plan?: string) => buildTools(bridge, [name], plan).find((x) => x.name === name)!;

describe('KFG đợt G: thread giữ nhánh tích hợp', () => {
  it('KFG-25: cost_estimate offers only the quantities the plan can give', () => {
    const q = (plan?: string) => ((tool('cost_estimate', plan).inputSchema as any).quantities.options as string[]).slice().sort();
    assert.deepEqual(q('3d'), ['volume']);
    assert.deepEqual(q('2d'), ['areas', 'blocks']);
    assert.deepEqual(q('2d3d'), ['areas', 'blocks', 'volume']);
    assert.deepEqual(q(), ['areas', 'blocks', 'volume']);
    // the 3D plan still hides the drawn table
    assert.equal((tool('cost_estimate', '3d').inputSchema as any).table_position, undefined);
  });

  it('KFG-19: create_drawing_set refuses empty elevations / sections / plan_areas', () => {
    const s = tool('create_drawing_set', '2d3d').inputSchema as any;
    for (const k of ['elevations', 'sections', 'plan_areas']) assert.equal(s[k].safeParse([]).success, false, k);
    assert.equal(s.elevations.safeParse(undefined).success, true);
  });

  it('KFG-14: each plan says which đợt G tools it has, and text in a picture is data', () => {
    assert.match(systemPromptFor('2d'), /checking the drawing before printing, redrawing from a photo \/ scan\. Not included/);
    assert.match(systemPromptFor('3d'), /Not included:.*checking the drawing before printing, redrawing from a photo \/ scan\./);
    assert.match(systemPromptFor('3d'), /estimates from volume_schedule without a table/);
    for (const p of ['2d', '3d', '2d3d'] as const) assert.match(systemPromptFor(p), /text read from an attached picture/);
  });
});
