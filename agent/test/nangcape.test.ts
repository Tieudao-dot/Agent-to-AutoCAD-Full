/**
 * Nâng cấp đợt E (13/11/2026, docs/ban-giao/nang-cap-e.md): build_stair and build_roof as Claude sees them (plan
 * labels, schemas, defaults agreed with the project owner, time-out, no confirmation card: they only add solids).
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { z } from 'zod';
import { affectedCount, confirmationError, createdHandles, existingSolidsChanged, type GateState } from '../src/agent/confirmation.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import { timeoutFor } from '../src/ipc/rpc.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';
import { STAIR_ROOF, STAIR_ROOF_TOOL_NAMES } from '../src/tools/solids3.js';

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
const none: GateState = { userText: 'dựng cầu thang và mái', answersQuestion: false };

describe('nâng cấp đợt E: stairs and roofs', () => {
  it('both tools are 3D: the 3D and Full plans see them, the 2D plan does not', () => {
    const L = labels();
    const on3d = buildTools(bridge, planTools('3d'), '3d').map((t) => t.name);
    const on2d = buildTools(bridge, planTools('2d'), '2d').map((t) => t.name);
    const full = buildTools(bridge).map((t) => t.name);
    for (const t of STAIR_ROOF_TOOL_NAMES) {
      assert.equal(L.get(t), 'ThreeD', t);
      assert.ok(on3d.includes(t), t);
      assert.ok(full.includes(t), t);
      assert.ok(!on2d.includes(t), t);
    }
  });

  it('schemas: the three stair types and three roof types, height required', () => {
    const tools = new Map(buildTools(bridge).map((t) => [t.name, t]));
    const sch = (n: string) => z.object(tools.get(n)!.inputSchema as z.ZodRawShape);
    assert.ok(sch('build_stair').safeParse({ type: 'straight', height: 3300, start: [0, 0], direction: 90 }).success);
    assert.ok(sch('build_stair').safeParse({ type: 'l', height: 3300, start: [0, 0], turn: 'right', railing: 'both' }).success);
    assert.ok(sch('build_stair').safeParse({ type: 'u', height: 3600, well_handle: '2AF', riser: 170, tread: 260, width: 1100 }).success);
    assert.ok(!sch('build_stair').safeParse({ type: 'u', start: [0, 0] }).success, 'height is required');
    assert.ok(!sch('build_stair').safeParse({ type: 'spiral', height: 3300, start: [0, 0] }).success);
    assert.ok(!sch('build_stair').safeParse({ height: 3300, start: [0, 0], riser: 400 }).success);
    for (const type of ['flat', 'gable', 'hip']) assert.ok(sch('build_roof').safeParse({ type, from_walls: true }).success, type);
    assert.ok(sch('build_roof').safeParse({ type: 'hip', outline_handle: '1F', slope: 35, overhang: 800, thickness: 120, wall_top_z: 6600 }).success);
    assert.ok(!sch('build_roof').safeParse({ type: 'hip', from_walls: true, slope: 75 }).success);
    assert.ok(!sch('build_roof').safeParse({ from_walls: true }).success, 'type is required');
  });

  it('the agreed defaults are in the descriptions and match the plugin (Core/StairRoof.cs)', () => {
    const src = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'StairRoof.cs'), 'utf8');
    const E = STAIR_ROOF;
    assert.match(src, new RegExp(`DefaultRiser = ${E.DefaultRiser}, DefaultTread = ${E.DefaultTread}, DefaultWidth = ${E.DefaultWidth};`));
    assert.match(src, new RegExp(`DefaultWaist = ${E.DefaultWaist}, DefaultGap = ${E.DefaultGap};`));
    assert.match(src, new RegExp(`MinRiser = ${E.MinRiser}, MaxRiser = ${E.MaxRiser}, MinTread = ${E.MinTread}, MaxTread = ${E.MaxTread};`));
    assert.match(src, new RegExp(`MinWidth = ${E.MinWidth}, MaxWidth = ${E.MaxWidth}, MinHeight = ${E.MinHeight}, MaxHeight = ${E.MaxHeight};`));
    assert.match(src, new RegExp(`RailHeight = ${E.RailHeight},`));
    assert.match(src, new RegExp(`Layer3d = "${E.StairLayer}";`));
    assert.match(src, new RegExp(`DefaultThickness = ${E.DefaultRoofThickness}, DefaultSlopeDeg = ${E.DefaultSlopeDeg}, DefaultOverhang = ${E.DefaultOverhang};`));
    assert.match(src, new RegExp(`MinThickness = ${E.MinRoofThickness}, MaxThickness = ${E.MaxRoofThickness}, MinSlopeDeg = ${E.MinSlopeDeg}, MaxSlopeDeg = ${E.MaxSlopeDeg}, MaxOverhang = ${E.MaxOverhang};`));
    assert.match(src, new RegExp(`Layer3d = "${E.RoofLayer}";`));
    const d = new Map(buildTools(bridge).map((t) => [t.name, t.description]));
    for (const n of ['160', '280', '1000']) assert.match(d.get('build_stair')!, new RegExp(n));
    for (const n of ['150', '30°', '600']) assert.match(d.get('build_roof')!, new RegExp(n));
  });

  it('they get the long time-out and a line in the system prompt', () => {
    for (const t of STAIR_ROOF_TOOL_NAMES) assert.equal(timeoutFor(t, {}), 120_000, t);
    const sp = systemPromptFor('3d');
    assert.match(sp, /build_stair/);
    assert.match(sp, /build_roof/);
  });

  it('they only add solids: no confirmation card, the new solids count as created', () => {
    for (const t of STAIR_ROOF_TOOL_NAMES) {
      const p = { well_handle: 'A1', outline_handle: 'A2', height: 3300, type: t === 'build_roof' ? 'hip' : 'u' };
      assert.equal(affectedCount(t, p), 0, t);
      assert.deepEqual(existingSolidsChanged(t, p, none), [], t);
      assert.equal(confirmationError(t, p, none), undefined, t);
    }
    assert.deepEqual(createdHandles('build_stair', { type: 'u', height: 3300, start: [0, 0], railing: 'both' }, { handle: 'S1', handles: ['S1', 'R1'] }), ['S1', 'R1']);
  });
});
