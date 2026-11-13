/**
 * Nâng cấp đợt B (13/11/2026, docs/ban-giao/nang-cap-b.md): the new 2D editing tools and the upgraded ones, as Claude
 * sees them, and their place in the confirmation gate (the 2D rule: more than 20 existing objects asks first).
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { z } from 'zod';
import { affectedCount, affectedHandles, confirmationError, createdHandles } from '../src/agent/confirmation.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');
const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const tools = new Map(buildTools(bridge).map((t) => [t.name, t]));
const schema = (name: string) => z.object(tools.get(name)!.inputSchema as z.ZodRawShape);

const NEW_2D = ['stretch_entities', 'break_entity', 'lengthen_entity', 'divide_entity'];
const NEW_COMMON = ['group_entities', 'match_properties'];

function labels(): Map<string, string> {
  const src = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'Plans.cs'), 'utf8');
  const out = new Map<string, string>();
  for (const m of src.matchAll(/Add\(ToolGroup\.(\w+),([\s\S]*?)\);/g))
    for (const s of m[2]!.matchAll(/"([^"]*)"/g)) for (const n of s[1]!.split(/\s+/)) if (n) out.set(n, m[1]!);
  return out;
}

describe('nâng cấp đợt B: tools', () => {
  it('the six new tools exist with the plan labels decided on 13/11 (phương án A)', () => {
    const L = labels();
    for (const t of NEW_2D) {
      assert.ok(tools.has(t), t);
      assert.equal(L.get(t), 'TwoD', t);
    }
    for (const t of NEW_COMMON) {
      assert.ok(tools.has(t), t);
      assert.equal(L.get(t), 'Common', t);
    }
  });

  it('upgraded tools keep their names and the old calls still validate (recorded macros, habits)', () => {
    assert.ok(schema('fillet_entities').safeParse({ handle1: 'A1', handle2: 'B2', radius: 5 }).success);
    assert.ok(schema('chamfer_entities').safeParse({ handle1: 'A1', handle2: 'B2', dist1: 5 }).success);
    assert.ok(schema('offset_entity').safeParse({ handle: 'A1', distance: 220, side_point: [0, 10] }).success);
    assert.ok(schema('join_entities').safeParse({ handles: ['A1', 'B2'] }).success);
  });

  it('new parameters: polyline corners, both sides, tolerance', () => {
    assert.ok(schema('fillet_entities').safeParse({ handle1: 'A1', radius: 5 }).success);
    assert.ok(schema('fillet_entities').safeParse({ handle1: 'A1', radius: 5, vertex: 2 }).success);
    assert.ok(!schema('fillet_entities').safeParse({ handle1: 'A1', radius: 5, vertex: -1 }).success);
    assert.ok(schema('chamfer_entities').safeParse({ handle1: 'A1', dist1: 5, dist2: 3 }).success);
    assert.ok(schema('offset_entity').safeParse({ handle: 'A1', distance: 110, both_sides: true }).success);
    assert.ok(schema('join_entities').safeParse({ handles: ['A1', 'B2'], tolerance: 0.5 }).success);
    assert.ok(!schema('join_entities').safeParse({ handles: ['A1', 'B2'], tolerance: -1 }).success);
    assert.match(tools.get('fillet_entities')!.description, /LWPOLYLINE/);
    assert.match(tools.get('fillet_entities')!.description, /ARC/);
  });

  it('new tools validate their main forms', () => {
    assert.ok(schema('stretch_entities').safeParse({ corner1: [0, 0], corner2: [10, 10], displacement: [100, 0] }).success);
    assert.ok(schema('stretch_entities').safeParse({ handles: ['A1'], polygon: [[0, 0], [1, 0], [0, 1]], from: [0, 0], to: [5, 0] }).success);
    assert.ok(schema('break_entity').safeParse({ handle: 'A1', point: [5, 0] }).success);
    assert.ok(schema('break_entity').safeParse({ handle: 'A1', point: [5, 0], point2: [8, 0] }).success);
    assert.ok(schema('lengthen_entity').safeParse({ handle: 'A1', value: -20 }).success);
    assert.ok(schema('lengthen_entity').safeParse({ handle: 'A1', mode: 'percent', value: 150, near_point: [0, 0] }).success);
    assert.ok(!schema('lengthen_entity').safeParse({ handle: 'A1', mode: 'scale', value: 2 }).success);
    assert.ok(schema('divide_entity').safeParse({ handle: 'A1', count: 5 }).success);
    assert.ok(schema('divide_entity').safeParse({ handle: 'A1', segment_length: 600, block_name: 'COC', from: 'end' }).success);
    assert.ok(schema('group_entities').safeParse({ handles: ['A1', 'B2'], name: 'BAN GHE' }).success);
    assert.ok(schema('group_entities').safeParse({ action: 'ungroup', name: 'BAN GHE' }).success);
    assert.ok(schema('match_properties').safeParse({ source: 'A1', handles: ['B2'] }).success);
    assert.ok(schema('match_properties').safeParse({ source: 'A1', handles: ['B2'], properties: ['layer', 'text'] }).success);
    assert.ok(!schema('match_properties').safeParse({ source: 'A1', handles: ['B2'], properties: ['font'] }).success);
  });

  it('match_properties tells Claude that text / dimension properties need 2D', () => {
    assert.match(tools.get('match_properties')!.description, /text \/ dimension only on plans with 2D/);
  });
});

describe('nâng cấp đợt B: confirmation gate (the 2D rule, not loosened)', () => {
  const g = { userText: 'kéo giãn', answersQuestion: false };
  const many = Array.from({ length: 21 }, (_, i) => `A${i}`);

  it('tools that change existing objects count them', () => {
    assert.equal(affectedCount('stretch_entities', { handles: many }), 21);
    assert.equal(affectedCount('stretch_entities', { corner1: [0, 0], corner2: [1, 1], displacement: [1, 0] }), 0); // the plugin asks above 20
    assert.equal(affectedCount('break_entity', { handle: 'A1', point: [0, 0] }), 1);
    assert.equal(affectedCount('lengthen_entity', { handle: 'A1', value: 5 }), 1);
    assert.equal(affectedCount('match_properties', { source: 'S', handles: many }), 21);
    assert.equal(affectedCount('fillet_entities', { handle1: 'P1', radius: 5 }), 1); // a polyline alone
    assert.deepEqual(affectedHandles('fillet_entities', { handle1: 'p1', radius: 5 }), ['P1']);
    // creating only: not counted
    assert.equal(affectedCount('divide_entity', { handle: 'A1', count: 4 }), 0);
    assert.equal(affectedCount('group_entities', { handles: many }), 0);
    assert.equal(affectedCount('offset_entity', { handle: 'A1', distance: 5, both_sides: true }), 0);
  });

  it('more than 20 objects asks first, a yes lets it through', () => {
    assert.match(confirmationError('stretch_entities', { handles: many, displacement: [1, 0] }, g) ?? '', /Confirmation required/);
    assert.match(confirmationError('match_properties', { source: 'S', handles: many }, g) ?? '', /Confirmation required/);
    assert.equal(confirmationError('match_properties', { source: 'S', handles: many.slice(0, 5) }, g), undefined);
    assert.equal(confirmationError('stretch_entities', { handles: many, displacement: [1, 0] }, { userText: 'đồng ý', answersQuestion: true }), undefined);
    const calls = Array.from({ length: 21 }, (_, i) => ({ tool: 'break_entity', params: { handle: `L${i}`, point: [0, 0] } }));
    assert.match(confirmationError('batch_execute', { calls }, g) ?? '', /Confirmation required/);
  });

  it('marks of divide_entity are new objects of the turn', () => {
    assert.deepEqual(createdHandles('divide_entity', { handle: 'A1', count: 3 }, { handles: ['b1', 'b2'] }), ['B1', 'B2']);
    assert.deepEqual(createdHandles('break_entity', { handle: 'A1', point: [0, 0] }, { handles: ['b1', 'b2'] }), []);
  });
});
