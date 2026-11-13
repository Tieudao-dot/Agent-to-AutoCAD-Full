/**
 * Nâng cấp đợt D (13/11/2026, docs/ban-giao/nang-cap-d.md): the 3D additions as Claude sees them (plan labels, schemas,
 * the table only on Full) and PRESSPULL in the 3D confirmation gate.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { z } from 'zod';
import { affectedCount, confirmationError, createdHandles, existingSolidsChanged, type GateState } from '../src/agent/confirmation.js';
import { timeoutFor } from '../src/ipc/rpc.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';
import { SOLID3, SOLID3_TOOL_NAMES } from '../src/tools/solids3.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');
const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };

function labels(): Map<string, string> {
  const src = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'Plans.cs'), 'utf8');
  const out = new Map<string, string>();
  for (const m of src.matchAll(/Add\(ToolGroup\.(\w+),([\s\S]*?)\);/g))
    for (const s of m[2]!.matchAll(/"([^"]*)"/g)) for (const n of s[1]!.split(/\s+/)) if (n) out.set(n, m[1]!);
  return out;
}
const planTools = (plan: '2d' | '3d') => {
  const L = labels();
  return [...L].filter(([, g]) => g === 'Common' || (plan === '3d' ? g === 'ThreeD' : g === 'TwoD')).map(([n]) => n);
};
const shapeOf = (plan: '2d' | '3d' | '2d3d' | undefined, name: string) => {
  const t = buildTools(bridge, plan === '2d3d' || plan === undefined ? undefined : planTools(plan), plan).find((x) => x.name === name);
  return t ? (t.inputSchema as Record<string, unknown>) : undefined;
};

const none: GateState = { userText: 'kéo mặt trên khối lên 500', answersQuestion: false };
const typedYes: GateState = { userText: 'ok', answersQuestion: true };
const card = (hs: string[]): GateState => ({ userText: '', answersQuestion: true, confirmedByButton: true, cardConfirmed: new Set(hs) });

describe('nâng cấp đợt D: tools and plan labels', () => {
  it('the four new tools are 3D: the 3D and Full plans see them, the 2D plan does not', () => {
    const L = labels();
    for (const t of SOLID3_TOOL_NAMES) assert.equal(L.get(t), 'ThreeD', t);
    const on3d = buildTools(bridge, planTools('3d'), '3d').map((t) => t.name);
    const on2d = buildTools(bridge, planTools('2d'), '2d').map((t) => t.name);
    const full = buildTools(bridge).map((t) => t.name);
    for (const t of SOLID3_TOOL_NAMES) {
      assert.ok(on3d.includes(t), t);
      assert.ok(full.includes(t), t);
      assert.ok(!on2d.includes(t), t);
    }
  });

  it('a table in the drawing is Full only: the 3D plan does not see table_position (numbers only)', () => {
    for (const t of ['volume_schedule', 'check_interference']) {
      assert.ok('table_position' in shapeOf(undefined, t)!, `${t} on Full`);
      assert.ok('table_position' in shapeOf('2d3d', t)!, `${t} on 2d3d`);
      const s3 = shapeOf('3d', t)!;
      assert.ok(!('table_position' in s3), `${t} on 3D`);
      assert.ok('handles' in s3, `${t} keeps its other parameters`);
    }
    const d3 = buildTools(bridge, planTools('3d'), '3d').find((x) => x.name === 'check_interference')!.description;
    assert.ok(!/table_position/.test(d3), d3);
    assert.match(d3, /in the chat/);
  });

  it('schemas: old check_interference calls still validate; beam / presspull / stl / schedule', () => {
    const tools = new Map(buildTools(bridge).map((t) => [t.name, t]));
    const sch = (n: string) => z.object(tools.get(n)!.inputSchema as z.ZodRawShape);
    assert.ok(sch('check_interference').safeParse({ handles: ['A1', 'A2'] }).success);
    assert.ok(sch('check_interference').safeParse({ handles: ['A1'], other_handles: ['B1'], table_position: [0, 0], decimals: 2 }).success);
    assert.ok(sch('build_beam').safeParse({ path_handles: ['1F'], width: 220, height: 500, top_z: 3300, slab_thickness: 120 }).success);
    assert.ok(sch('build_beam').safeParse({ path_points: [[0, 0], [4000, 0], [4000, 3000]], path_bulges: [0, 0.4] }).success);
    assert.ok(!sch('build_beam').safeParse({ path_handles: ['1F'], width: 10 }).success);
    assert.ok(sch('presspull').safeParse({ handle: 'A1', face_filter: ['top'], distance: 500 }).success);
    assert.ok(sch('presspull').safeParse({ profile_points: [[0, 0], [100, 0], [100, 100]], distance: -50, target: 'A1', mode: 'subtract' }).success);
    assert.ok(!sch('presspull').safeParse({ handle: 'A1', face_filter: ['top'] }).success, 'distance is required');
    assert.ok(sch('export_stl').safeParse({ output: 'mo-hinh.stl', ascii: true }).success);
    assert.ok(sch('volume_schedule').safeParse({ layers: ['A-*-3D'], overlap: 'deduct', priority: ['A-COLS-3D'] }).success);
    assert.ok(!sch('volume_schedule').safeParse({ overlap: 'sometimes' }).success);
  });

  it('defaults match the plugin (Core/Solids3.cs)', () => {
    const src = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'Solids3.cs'), 'utf8');
    assert.match(src, new RegExp(`DefaultBeamWidth = ${SOLID3.DefaultBeamWidth}, DefaultBeamHeight = ${SOLID3.DefaultBeamHeight};`));
    assert.match(src, new RegExp(`MinBeamSize = ${SOLID3.MinBeamSize}, MaxBeamWidth = ${SOLID3.MaxBeamWidth}, MaxBeamHeight = ${SOLID3.MaxBeamHeight};`));
    assert.match(src, new RegExp(`MaxBeamPaths = ${SOLID3.MaxBeamPaths}, MaxBeamSegments = ${SOLID3.MaxBeamSegments};`));
    assert.match(src, new RegExp(`BeamLayer = "${SOLID3.BeamLayer}";`));
    assert.match(src, new RegExp(`MaxScheduleSolids = ${SOLID3.MaxScheduleSolids};`));
    assert.match(src, new RegExp(`MaxStlSolids = ${SOLID3.MaxStlSolids};`));
  });

  it('beams, schedules and STL get the long time-out (AutoCAD unites / measures many solids)', () => {
    for (const t of ['build_beam', 'volume_schedule', 'export_stl']) assert.equal(timeoutFor(t, {}), 120_000, t);
  });
});

describe('nâng cấp đợt D: PRESSPULL in the 3D confirmation gate', () => {
  it('pulling faces of a solid older than the turn needs the card (a typed ok does not)', () => {
    const p = { handle: 'A1', face_filter: ['top'], distance: 500 };
    assert.deepEqual(existingSolidsChanged('presspull', p, none), ['A1']);
    assert.match(confirmationError('presspull', p, none) ?? '', /Confirmation required/);
    assert.match(confirmationError('presspull', p, typedYes) ?? '', /Confirmation required/);
    assert.equal(confirmationError('presspull', p, card(['A1'])), undefined);
    // a solid built in this turn needs none
    assert.equal(confirmationError('presspull', p, { ...none, created: new Set(['A1']) }), undefined);
    // also inside batch_execute
    assert.match(confirmationError('batch_execute', { calls: [{ tool: 'presspull', params: p }] }, none) ?? '', /Confirmation required/);
  });

  it('a profile added to / cut from an existing target needs the card; a new solid does not', () => {
    const prof = { profile_points: [[0, 0], [100, 0], [100, 100]], distance: 200 };
    assert.deepEqual(existingSolidsChanged('presspull', { ...prof, target: 'B2' }, none), ['B2']);
    assert.deepEqual(existingSolidsChanged('presspull', prof, none), []);
    assert.deepEqual(existingSolidsChanged('presspull', { ...prof, target: 'B2', mode: 'new' }, none), []);
    assert.equal(affectedCount('presspull', prof), 0);
    // erased profile curves follow the usual count rule, not the 3D card
    assert.deepEqual(existingSolidsChanged('presspull', { profile_handles: ['C1'], erase_profile: true, distance: 100 }, none), []);
  });

  it('a new solid from a profile is "created" (Claude may change it in the same turn); the target is not', () => {
    const prof = { profile_points: [[0, 0], [100, 0], [100, 100]], distance: 200 };
    assert.deepEqual(createdHandles('presspull', prof, { handles: ['N1'] }), ['N1']);
    assert.deepEqual(createdHandles('presspull', { ...prof, target: 'B2' }, { handle: 'B2', handles: ['B2'] }), []);
  });

  it('beams, STL and the schedules never change existing objects', () => {
    for (const t of ['build_beam', 'export_stl', 'volume_schedule'])
      assert.equal(affectedCount(t, { handles: ['A1'], path_handles: ['A1'], table_position: [0, 0] }), 0, t);
  });
});
