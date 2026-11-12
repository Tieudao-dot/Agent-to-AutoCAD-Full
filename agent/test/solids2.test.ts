/**
 * 3D phase 2 (docs/ban-giao/ve-3d-dot-2.md): the tool declarations, the confirmation gate for editing existing solids
 * (slice / fillet / chamfer / shell always, the 3D transforms like move / rotate / mirror), the limits shared with the
 * plugin and the phase 2 rules of the system prompt.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { z } from 'zod';
import { affectedCount, confirmationError, createdHandles, existingSolidsChanged, type GateState } from '../src/agent/confirmation.js';
import { THREE_D_RULES, systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';
import { EDGE_FILTERS, FACE_SIDES, SOLID2_LIMITS, SOLID_EDIT_TOOLS, SOLID_TOOL_NAMES, UCS_PRESETS } from '../src/tools/solids.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');

const calls: Array<{ tool: string; params: Record<string, unknown> }> = [];
const bridge: ToolBridge = {
  call: async (tool, params) => {
    calls.push({ tool, params });
    return { id: 'x', ok: true, result: { handles: ['1A'] } };
  },
  status: () => {},
};
const tools = buildTools(bridge);
const byName = (n: string) => tools.find((t) => t.name === n)!;
const ok = (n: string, p: unknown) => z.object(byName(n).inputSchema as z.ZodRawShape).safeParse(p).success;

const PHASE2 = ['sweep', 'loft', 'slice_solid', 'fillet_edges', 'chamfer_edges', 'shell_solid', 'rotate_3d', 'mirror_3d', 'align', 'set_ucs', 'reset_ucs', 'check_interference'];

describe('3D phase 2 tools', () => {
  it('are declared, in the 3D list, and a 2D machine does not load them', () => {
    for (const n of PHASE2) {
      assert.ok(byName(n), n);
      assert.ok((SOLID_TOOL_NAMES as readonly string[]).includes(n), n);
    }
    const twoD = tools.map((t) => t.name).filter((n) => !(SOLID_TOOL_NAMES as readonly string[]).includes(n));
    const kept = buildTools(bridge, twoD).map((t) => t.name);
    for (const n of PHASE2) assert.ok(!kept.includes(n), n);
  });

  it('schemas accept typical calls and reject bad ones', () => {
    assert.ok(ok('sweep', { profile_points: [[0, 0], [50, 0], [50, 50], [0, 50]], path_points: [[0, 0, 0], [0, 0, 1000], [1000, 0, 1000]] }));
    assert.ok(ok('sweep', { profile_handles: ['2F'], path_handle: '3A', twist: 90, scale: 0.5 }));
    assert.ok(!ok('sweep', { profile_handles: ['2F'], path_handle: '3A', scale: 0 }));
    assert.ok(!ok('sweep', { profile_handles: ['2F'], path_handle: '3A', twist: 4000 }));
    assert.ok(ok('loft', { sections_points: [[[0, 0, 0], [1000, 0, 0], [1000, 1000, 0], [0, 1000, 0]], [[400, 400, 2000], [600, 400, 2000], [600, 600, 2000]]] }));
    assert.ok(!ok('loft', { section_handles: ['A'] }));
    assert.ok(ok('slice_solid', { handle: '2F4A', point: [2000, 0, 0], plane: 'yz' }));
    assert.ok(ok('slice_solid', { handle: '2F4A', plane_points: [[0, 0, 0], [1, 0, 0], [0, 0, 1]], keep: 'negative' }));
    assert.ok(!ok('slice_solid', { handle: '2F4A', point: [0, 0], plane: 'xyz' }));
    assert.ok(ok('fillet_edges', { handle: '2F4A', radius: 50, edge_filter: ['parallel_z'] }));
    assert.ok(ok('fillet_edges', { handle: '2F4A', radius: 50, edges: [0, 3], topology_id: '0123456789abcdef' }));
    assert.ok(!ok('fillet_edges', { handle: '2F4A', radius: 0, edge_filter: ['all'] }));
    assert.ok(!ok('fillet_edges', { handle: '2F4A', radius: 5, edge_filter: ['vertical'] }));
    assert.ok(!ok('fillet_edges', { handle: '2F4A', radius: 5, edges: [-1] }));
    assert.ok(ok('chamfer_edges', { handle: '2F4A', distance: 20, distance2: 30, base_face: 1, edges: [2], topology_id: 'x' }));
    assert.ok(ok('shell_solid', { handle: '2F4A', thickness: 20, face_filter: ['top'] }));
    assert.ok(ok('shell_solid', { handle: '2F4A', thickness: 20 }));
    assert.ok(!ok('shell_solid', { handle: '2F4A', thickness: -2 }));
    assert.ok(ok('rotate_3d', { handles: ['A'], axis_start: [0, 0, 0], axis_end: [1, 0, 0], angle: 90 }));
    assert.ok(ok('mirror_3d', { handles: ['A'], point: [0, 0, 0], normal: [1, 0, 0], keep_original: false }));
    assert.ok(ok('align', { handles: ['A'], source_points: [[0, 0], [1, 0]], dest_points: [[5, 5], [5, 6]], scale: true }));
    assert.ok(!ok('align', { handles: ['A'], source_points: [[0, 0], [1, 0], [0, 1], [1, 1]], dest_points: [[0, 0]] }));
    assert.ok(ok('set_ucs', { preset: 'front', origin: [0, 0, 0], plan_view: true }));
    assert.ok(ok('set_ucs', { origin: [0, 0, 0], x_axis: [1, 0, 0], y_axis: [0, 0, 1], save_as: 'Mặt đứng' }));
    assert.ok(!ok('set_ucs', { preset: 'iso' }));
    assert.ok(ok('reset_ucs', {}));
    assert.ok(ok('check_interference', { handles: ['A', 'B'] }));
    assert.ok(ok('check_interference', { handles: ['A'], other_handles: ['B', 'C'] }));
    assert.ok(ok('get_solid_info', { handles: ['A'], edges: true, faces: true, max_items: 300 }));
    assert.ok(!ok('get_solid_info', { handles: ['A'], edges: true, max_items: 301 }));
  });

  it('limits and filter names match the plugin (Core/Solids2.cs)', () => {
    const src = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'Solids2.cs'), 'utf8');
    for (const [k, v] of Object.entries(SOLID2_LIMITS)) assert.match(src, new RegExp(`\\b${k} = ${String(v).replace('.', '\\.')};`), k);
    for (const f of EDGE_FILTERS) assert.ok(src.includes(`"${f}"`), f);
    for (const f of FACE_SIDES) assert.ok(src.includes(`"${f}"`), f);
    for (const u of UCS_PRESETS) assert.ok(src.includes(`"${u}"`), u);
  });

  it('forwards indices, topology_id and filters unchanged', async () => {
    await byName('fillet_edges').handler({ handle: '2F4A', radius: 50, edges: [1, 2], topology_id: 'abc' }, {});
    assert.deepEqual(calls.at(-1), { tool: 'fillet_edges', params: { handle: '2F4A', radius: 50, edges: [1, 2], topology_id: 'abc' } });
  });
});

describe('3D phase 2 confirmation gate', () => {
  const first = (extra: Partial<GateState> = {}): GateState => ({ userText: 'bo tròn các cạnh đứng', answersQuestion: false, touched: new Set(), created: new Set(), ...extra });

  it('slice / fillet / chamfer / shell of a solid from before the turn always need a yes', () => {
    for (const t of SOLID_EDIT_TOOLS) {
      const err = confirmationError(t, { handle: '2f4a', radius: 5, distance: 5, thickness: 5, point: [0, 0], plane: 'xy' }, first());
      assert.match(err ?? '', /Confirmation required: .*1 3D solid.*2F4A/, t);
      assert.match(err ?? '', /request_confirmation/, t);
      // built in this turn: none
      assert.equal(confirmationError(t, { handle: '2F4A' }, first({ created: new Set(['2F4A']) })), undefined, t);
      // after the user's yes
      assert.equal(confirmationError(t, { handle: '2F4A' }, first({ userText: 'đồng ý', answersQuestion: true })), undefined, t);
      assert.equal(confirmationError(t, { handle: '2F4A' }, first({ confirmedByButton: true })), undefined, t);
    }
  });

  it('is enforced inside batch_execute', () => {
    const batch = { calls: [{ tool: 'draw_box', params: { length: 1, width: 1, height: 1 } }, { tool: 'fillet_edges', params: { handle: '9C', radius: 5, edge_filter: ['all'] } }] };
    assert.deepEqual(existingSolidsChanged('batch_execute', batch, first()), ['9C']);
    assert.match(confirmationError('batch_execute', batch, first()) ?? '', /Confirmation required/);
  });

  it('3D transforms follow the move / rotate / mirror rule (more than 20 existing objects)', () => {
    const few = ['A', 'B'];
    const many = Array.from({ length: 25 }, (_, i) => (i + 1).toString(16));
    assert.equal(confirmationError('rotate_3d', { handles: few, axis_start: [0, 0], axis_end: [1, 0], angle: 90 }, first()), undefined);
    assert.match(confirmationError('rotate_3d', { handles: many }, first()) ?? '', /more than 20/);
    assert.match(confirmationError('align', { handles: many }, first()) ?? '', /more than 20/);
    // mirror_3d copies by default: nothing existing changes
    assert.equal(affectedCount('mirror_3d', { handles: many }), 0);
    assert.match(confirmationError('mirror_3d', { handles: many, keep_original: false }, first()) ?? '', /more than 20/);
    // a single existing solid rotated: no card (unlike fillet)
    assert.deepEqual(existingSolidsChanged('rotate_3d', { handles: ['2F4A'] }, first()), []);
  });

  it('slice keeping both halves: only the new half is "created"', () => {
    assert.deepEqual(createdHandles('slice_solid', { handle: '2f4a', point: [0, 0], plane: 'xy' }, { handle: '2F4A', handles: ['2F4A', '3B'], created: ['3B'] }), ['3B']);
    assert.deepEqual(createdHandles('fillet_edges', { handle: '2F4A', radius: 5 }, { handle: '2F4A', handles: ['2F4A'] }), []);
    // sweep / loft make new solids; erasing an existing profile counts
    assert.deepEqual(createdHandles('sweep', { profile_handles: ['A'], path_handle: 'B', erase_profile: true }, { handles: ['3C'] }), ['3C']);
    assert.equal(affectedCount('loft', { section_handles: ['A', 'B', 'C'], erase_profile: true }), 3);
    assert.equal(affectedCount('loft', { section_handles: ['A', 'B', 'C'] }), 0);
    assert.deepEqual(createdHandles('mirror_3d', { handles: ['A'] }, { handles: ['5D'] }), ['5D']);
    // inside a batch
    const batch = { calls: [{ tool: 'slice_solid', params: { handle: 'A1' } }] };
    assert.deepEqual(createdHandles('batch_execute', batch, { results: [{ ok: true, result: { handles: ['A1', 'B2'] } }] }), ['B2']);
  });

  it('read-only / view tools of phase 2 change nothing', () => {
    assert.equal(affectedCount('check_interference', { handles: ['A', 'B'] }), 0);
    assert.equal(affectedCount('set_ucs', { preset: 'front' }), 0);
    assert.equal(affectedCount('reset_ucs', {}), 0);
  });
});

describe('3D phase 2 system prompt', () => {
  it('only the 3D and Full plans get the rules, with the phase 2 conventions', () => {
    assert.ok(!systemPromptFor('2d').includes('topology_id'));
    assert.ok(systemPromptFor('3d').includes('topology_id'));
    for (const needle of ['get_solid_info with edges: true', 'edge_filter', 'never guess indices', 'slice_solid, fillet_edges, chamfer_edges, shell_solid', 'request_confirmation',
      'stay WCS', 'check_interference', 'capture_view (sw_iso, conceptual)', 'next phase'])
      assert.ok(THREE_D_RULES.includes(needle), needle);
    assert.ok(!THREE_D_RULES.includes('has no sweep'));
  });
});
