/**
 * Nâng cấp đợt C (13/11/2026, docs/ban-giao/nang-cap-c.md): the 2D utilities as Claude sees them (plan labels, schemas)
 * and find_replace_text in the confirmation gate (replacing always needs the card's Đồng ý, whatever the count).
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { z } from 'zod';
import {
  affectedCount, applyUserConfirmation, confirmationError, createdHandles, replaceNotApproved, USER_CONFIRMED_PARAM, type GateState,
} from '../src/agent/confirmation.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');
const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const tools = new Map(buildTools(bridge).map((t) => [t.name, t]));
const schema = (name: string) => z.object(tools.get(name)!.inputSchema as z.ZodRawShape);

const NEW_2D = ['dim_floor_plan', 'block_schedule', 'area_schedule', 'find_replace_text'];

function labels(): Map<string, string> {
  const src = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'Plans.cs'), 'utf8');
  const out = new Map<string, string>();
  for (const m of src.matchAll(/Add\(ToolGroup\.(\w+),([\s\S]*?)\);/g))
    for (const s of m[2]!.matchAll(/"([^"]*)"/g)) for (const n of s[1]!.split(/\s+/)) if (n) out.set(n, m[1]!);
  return out;
}

const none: GateState = { userText: 'thay chữ P.NGU thành PHÒNG NGỦ', answersQuestion: false };
const typedYes: GateState = { userText: 'đồng ý', answersQuestion: true };
const card = (hs: string[]): GateState => ({ userText: '', answersQuestion: true, confirmedByButton: true, cardConfirmed: new Set(hs) });

describe('nâng cấp đợt C: tools and plan labels', () => {
  it('the four new tools exist and are 2D (2D / Full plans, decided 12/11 + 13/11)', () => {
    const L = labels();
    for (const t of NEW_2D) {
      assert.ok(tools.has(t), t);
      assert.equal(L.get(t), 'TwoD', t);
    }
    // the upgraded ones keep their 2D label and their names
    assert.equal(L.get('draw_hatch'), 'TwoD');
    assert.equal(L.get('plot_pdf'), 'TwoD');
  });

  it('a 3D plan never sees them (the plugin sends only its plan\'s tools)', () => {
    const L = labels();
    const plan3d = [...L].filter(([, g]) => g !== 'TwoD').map(([n]) => n);
    const seen = buildTools(bridge, plan3d, '3d').map((t) => t.name);
    for (const t of [...NEW_2D, 'draw_hatch', 'plot_pdf']) assert.ok(!seen.includes(t), t);
  });

  it('old calls of draw_hatch and plot_pdf still validate; new parameters', () => {
    assert.ok(schema('draw_hatch').safeParse({ boundary_handles: ['A1'], pattern: 'ANSI31' }).success);
    assert.ok(schema('draw_hatch').safeParse({ boundary_points: [[0, 0], [1, 0], [1, 1]] }).success);
    assert.ok(schema('draw_hatch').safeParse({ pick_point: [500, 500], pattern: 'AR-CONC', islands: false, keep_boundary: true }).success);
    assert.ok(schema('plot_pdf').safeParse({ layout: 'A1' }).success);
    assert.ok(schema('plot_pdf').safeParse({ layouts: ['all'] }).success);
    assert.ok(schema('plot_pdf').safeParse({ layouts: ['A1', 'A2'], output: 'C:\\out\\bo.pdf' }).success);
    assert.ok(!schema('plot_pdf').safeParse({ layouts: [] }).success);
    assert.match(tools.get('plot_pdf')!.description, /ONE multi-page PDF/);
    assert.match(tools.get('draw_hatch')!.description, /pick_point/);
  });

  it('schemas of the new tools', () => {
    assert.ok(schema('dim_floor_plan').safeParse({}).success);
    assert.ok(schema('dim_floor_plan').safeParse({ wall_layers: ['TUONG'], axis_layers: ['TRUC'], sides: ['bottom', 'left'], offset: 800 }).success);
    assert.ok(!schema('dim_floor_plan').safeParse({ sides: ['front'] }).success);
    assert.ok(schema('block_schedule').safeParse({ names: ['CUA*'], group_by_attribute: 'MA', table_position: [0, 0] }).success);
    assert.ok(schema('area_schedule').safeParse({ group_by: 'room', layers: ['A-AREA'], table_position: [0, 0], decimals: 2 }).success);
    assert.ok(!schema('area_schedule').safeParse({ group_by: 'tầng' }).success);
    assert.ok(schema('find_replace_text').safeParse({ find: 'P.NGU', replace: 'PHÒNG NGỦ' }).success);
    assert.ok(schema('find_replace_text').safeParse({ find: 'X', replace: '' }).success, 'replace "" deletes');
    assert.ok(!schema('find_replace_text').safeParse({ find: 'X' }).success, 'replace is required');
    assert.ok(!schema('find_replace_text').safeParse({ find: '', replace: 'Y' }).success);
    // Claude cannot set the card parameter
    assert.ok(!(USER_CONFIRMED_PARAM in (tools.get('find_replace_text')!.inputSchema as object)));
  });
});

describe('nâng cấp đợt C: find_replace_text always through the card', () => {
  const apply = { find: 'P.NGU', replace: 'PHÒNG NGỦ', handles: ['a1', 'B2'] };

  it('the preview (no handles) changes nothing and asks nothing', () => {
    assert.equal(affectedCount('find_replace_text', { find: 'X', replace: 'Y' }), 0);
    assert.equal(confirmationError('find_replace_text', { find: 'X', replace: 'Y' }, none), undefined);
    // its match_handles are existing objects, never "created in this turn"
    assert.deepEqual(createdHandles('find_replace_text', { find: 'X', replace: 'Y' }, { objects: 2, match_handles: ['A1', 'B2'] }), []);
  });

  it('replacing even one text without the card is refused; a typed yes or the plan button does not count', () => {
    assert.match(confirmationError('find_replace_text', { ...apply, handles: ['A1'] }, none) ?? '', /Confirmation required: find_replace_text/);
    assert.match(confirmationError('find_replace_text', apply, typedYes) ?? '', /Confirmation required/);
    assert.match(confirmationError('find_replace_text', apply, { ...none, confirmedByButton: true }) ?? '', /Confirmation required/);
    // a card with only some of the handles
    assert.deepEqual(replaceNotApproved('find_replace_text', apply, card(['A1'])), ['B2']);
    assert.match(confirmationError('find_replace_text', apply, card(['A1'])) ?? '', /1 existing text/);
  });

  it('the card listing the handles lets it through and they count as changed objects', () => {
    assert.equal(confirmationError('find_replace_text', apply, card(['A1', 'B2'])), undefined);
    assert.equal(affectedCount('find_replace_text', apply), 2);
    // more than 20 on the card: the card is the question, so it passes too
    const many = Array.from({ length: 40 }, (_, i) => `F${i}`);
    assert.equal(confirmationError('find_replace_text', { ...apply, handles: many }, card(many)), undefined);
  });

  it('inside batch_execute too', () => {
    const batch = { calls: [{ tool: 'draw_line', params: { start: [0, 0], end: [1, 0] } }, { tool: 'find_replace_text', params: apply }] };
    assert.match(confirmationError('batch_execute', batch, typedYes) ?? '', /Confirmation required/);
    assert.equal(confirmationError('batch_execute', batch, card(['A1', 'B2'])), undefined);
  });

  it('the plugin gets user_confirmed = the call\'s handles on the card; whatever Claude sets is dropped', () => {
    assert.deepEqual(applyUserConfirmation('find_replace_text', apply, card(['A1', 'B2'])), { ...apply, user_confirmed: ['A1', 'B2'] });
    assert.deepEqual(applyUserConfirmation('find_replace_text', { ...apply, user_confirmed: ['A1', 'B2'] }, typedYes), apply);
    const batch = { calls: [{ tool: 'find_replace_text', params: { ...apply, user_confirmed: ['ZZ'] } }] };
    assert.deepEqual((applyUserConfirmation('batch_execute', batch, card(['A1'])) as { calls: unknown[] }).calls, [
      { tool: 'find_replace_text', params: { ...apply, user_confirmed: ['A1'] } },
    ]);
  });

  it('the plugin checks the same parameter (Util2DTools.cs) and macros never record it (Macros.cs)', () => {
    const src = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Drawing', 'Util2DTools.cs'), 'utf8');
    assert.ok(src.includes('SolidLogic.ConfirmedHandles(p.Get(SolidLogic.UserConfirmedParam))'));
    assert.ok(src.includes('Util2DLogic.ReplaceCardMessage'));
    const macros = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'Macros.cs'), 'utf8');
    assert.ok(macros.includes('copy.Remove(SolidLogic.UserConfirmedParam)'));
  });
});

describe('nâng cấp đợt C: new objects of the turn', () => {
  it('dimensions, hatches and schedule tables are created by the turn', () => {
    assert.deepEqual(createdHandles('dim_floor_plan', {}, { handles: ['d1', 'd2'], count: 2 }), ['D1', 'D2']);
    assert.deepEqual(createdHandles('draw_hatch', { pick_point: [1, 1] }, { handles: ['h1'] }), ['H1']);
    assert.deepEqual(createdHandles('block_schedule', { table_position: [0, 0] }, { total: 3, rows: [], handles: ['t1'] }), ['T1']);
    assert.deepEqual(createdHandles('area_schedule', {}, { total_m2: 3, rows: [{ name: 'P1', object: 'AA' }] }), []);
  });
});
