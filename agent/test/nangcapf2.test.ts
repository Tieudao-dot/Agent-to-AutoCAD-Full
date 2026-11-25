/**
 * Nâng cấp đợt F2 (25/11/2026, docs/ban-giao/nang-cap-f2.md): setup_standards, create_sheet (2D), insert_library_block,
 * layer_state (every plan). Plans / work modes load the right tools, overwrites need the card's Đồng ý, the schemas stay small.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { z } from 'zod';
import { fileURLToPath } from 'node:url';
import { USER_CONFIRMED_PARAM, applyUserConfirmation, confirmationError, refusedForConfirmation, type GateState } from '../src/agent/confirmation.js';
import { TWO_D_HINTS, systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';
import { standards2dTools } from '../src/tools/standards2d.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');
const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const NEW_2D = ['setup_standards', 'create_sheet'];
const NEW_COMMON = ['insert_library_block', 'layer_state'];

/** PlanLogic's labels (Core/Plans.cs), like crossplan.test.ts. */
function groups(): Map<string, string> {
  const src = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'Plans.cs'), 'utf8');
  const out = new Map<string, string>();
  for (const m of src.matchAll(/Add\(ToolGroup\.(\w+),([\s\S]*?)\);/g))
    for (const s of m[2]!.matchAll(/"([^"]*)"/g)) for (const n of s[1]!.split(/\s+/)) if (n) out.set(n, m[1]!);
  return out;
}
const G = groups();
const planTools = (plan: string) =>
  [...G.keys()].filter((t) => G.get(t) === 'Common' || (G.get(t) === 'TwoD' && plan !== '3d') || (G.get(t) === 'ThreeD' && plan !== '2d'));

describe('F2: plans and work modes', () => {
  it('2D / Full (and the 2D work mode) load all four, 3D (and the 3D work mode) only the library blocks and layer states', () => {
    for (const t of NEW_2D) assert.equal(G.get(t), 'TwoD', t);
    for (const t of NEW_COMMON) assert.equal(G.get(t), 'Common', t);
    const names = (plan: string) => buildTools(bridge, planTools(plan), plan).map((t) => t.name);
    for (const t of [...NEW_2D, ...NEW_COMMON]) assert.ok(names('2d').includes(t), t);
    for (const t of NEW_COMMON) assert.ok(names('3d').includes(t), t);
    for (const t of NEW_2D) assert.ok(!names('3d').includes(t), t);
    for (const t of [...NEW_2D, ...NEW_COMMON]) assert.ok(buildTools(bridge).some((x) => x.name === t), t);
  });

  it('the 2D hints name the new tools; the 3D prompt does not name the 2D ones', () => {
    assert.match(TWO_D_HINTS, /setup_standards/);
    assert.match(TWO_D_HINTS, /create_sheet/);
    assert.match(TWO_D_HINTS, /insert_library_block/);
    for (const t of NEW_2D) assert.doesNotMatch(systemPromptFor('3d'), new RegExp(`\\b${t}\\b`));
  });
});

describe('F2: overwriting needs the card', () => {
  const noCard: GateState = { userText: 'đồng ý', answersQuestion: true };
  const card: GateState = { userText: '', answersQuestion: true, confirmedByButton: true, cardConfirmed: new Set(['1A2B']) };

  for (const tool of ['setup_standards', 'layer_state']) {
    it(`${tool} with handles is refused without the card's Đồng ý, a typed yes does not count`, () => {
      const p = tool === 'layer_state' ? { action: 'save', name: 'IN', handles: ['1a2b'] } : { scale: '1:100', handles: ['1a2b'] };
      const err = confirmationError(tool, p, noCard);
      assert.ok(err?.startsWith('Confirmation required'), err);
      assert.match(err!, new RegExp(tool));
      assert.match(err!, /1A2B/);
      assert.ok(refusedForConfirmation({ ok: false, error: { message: err } }));
      // in a batch too
      const be = confirmationError('batch_execute', { calls: [{ tool: 'draw_line', params: {} }, { tool, params: p }] }, noCard);
      assert.ok(be?.startsWith('Confirmation required'), be);
      // the card that listed the handle: allowed, and the plugin gets user_confirmed
      assert.equal(confirmationError(tool, p, card), undefined);
      assert.deepEqual(applyUserConfirmation(tool, p, card)[USER_CONFIRMED_PARAM], ['1A2B']);
      assert.equal(USER_CONFIRMED_PARAM in applyUserConfirmation(tool, p, noCard), false);
      // Claude cannot set user_confirmed itself
      assert.equal(USER_CONFIRMED_PARAM in applyUserConfirmation(tool, { ...p, [USER_CONFIRMED_PARAM]: ['1A2B'] }, noCard), false);
    });
  }

  it('the calls that create or only read never ask', () => {
    assert.equal(confirmationError('setup_standards', { scale: '1:100' }, noCard), undefined);
    assert.equal(confirmationError('layer_state', { action: 'restore', name: 'IN' }, noCard), undefined);
    assert.equal(confirmationError('layer_state', { action: 'list' }, noCard), undefined);
    assert.equal(confirmationError('create_sheet', { paper: 'A3' }, noCard), undefined);
    assert.equal(confirmationError('insert_library_block', { name: 'cua_di', position: [0, 0] }, noCard), undefined);
  });

  it('find_replace_text keeps its own message', () => {
    const err = confirmationError('find_replace_text', { find: 'a', replace: 'b', handles: ['1A2B'] }, noCard);
    assert.match(err!, /find_replace_text would change 1 existing text/);
  });
});

describe('F2: small schemas (every tool is paid for at every chat start)', () => {
  it('the four tools together stay under 6 000 characters of description + schema', () => {
    const tools = standards2dTools(bridge);
    assert.equal(tools.length, 4);
    const size = tools.reduce((n, t) => n + t.name.length + t.description.length + JSON.stringify(z.toJSONSchema(z.object(t.inputSchema as z.ZodRawShape))).length, 0);
    assert.ok(size < 6000, String(size));
    for (const t of tools) assert.ok(t.description.length < 700, `${t.name}: ${t.description.length}`);
  });

  it('create_sheet offers A0 to A4 and the title fields; the library tool lists its names', () => {
    const tools = standards2dTools(bridge);
    const sheet = tools.find((t) => t.name === 'create_sheet')!;
    const schema = JSON.stringify(sheet.inputSchema);
    for (const p of ['A0', 'A1', 'A2', 'A3', 'A4', 'project', 'item', 'title', 'drawn_by', 'sheet_no']) assert.ok(schema.includes(p), p);
    const lib = tools.find((t) => t.name === 'insert_library_block')!;
    for (const n of ['cua_di', 'cua_so', 'bon_cau', 'giuong', 'cay']) assert.ok(lib.description.includes(n), n);
  });
});
