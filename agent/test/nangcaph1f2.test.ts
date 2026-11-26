/**
 * Nâng cấp đợt H1 (26/11/2026, docs/ban-giao/nang-cap-h1.md, part 4): the assistant tests of the unsaved patch of the
 * first F2 review thread (docs/ban-giao/f2-test-thread-con-ban-va.md), run against the current code. Cross checks of the F2 tools between the
 * assistant and the plugin: the plugin's refusal stops the turn, the schema matches what the plugin reads, and the
 * gate in a batch that mixes F2 overwrites with other calls.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { z } from 'zod';
import { confirmationError, refusedForConfirmation, type GateState } from '../src/agent/confirmation.js';
import type { ToolBridge } from '../src/tools/common.js';
import { standards2dTools } from '../src/tools/standards2d.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');
const read = (...p: string[]) => fs.readFileSync(path.join(repo, ...p), 'utf8');
const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const tools = standards2dTools(bridge);
const schemaOf = (name: string) => z.object(tools.find((t) => t.name === name)!.inputSchema as z.ZodRawShape);

describe('F2 test & sửa: the plugin and the assistant agree', () => {
  it("the plugin's refusal of an overwrite (no card) stops the turn like the assistant's own", () => {
    const core = read('plugin', 'ClaudeCAD.Plugin', 'Core', 'Standards2D.cs');
    const m = core.match(/\$"(Confirmation required: \{tool\}[^"]*)"/);
    assert.ok(m, 'F2Confirm.CardMessage starts with "Confirmation required"');
    const msg = m![1]!.replace('{tool}', 'layer_state').replace('{what}', "delete the layer state 'IN'");
    assert.ok(refusedForConfirmation({ ok: false, error: { message: msg } }));
    // also inside a batch result
    assert.ok(refusedForConfirmation({ ok: true, result: { results: [{ ok: true }, { ok: false, error: { message: msg } }] } }));
  });

  it('every parameter the plugin reads is in the schema (and nothing else)', () => {
    const src = read('plugin', 'ClaudeCAD.Plugin', 'Drawing', 'Standards2DTools.cs');
    const body = (fn: string) => {
      const i = src.indexOf(`internal static object ${fn}(`);
      const j = src.indexOf('internal static object ', i + 10);
      return src.slice(i, j < 0 ? undefined : j);
    };
    const reads = (fn: string) =>
      new Set([...body(fn).matchAll(/p\.(?:Str|Num|NumOpt|Bool|Get|Has|Point|PointOpt|Points|ObjOpt|Handles)\("([a-z_]+)"/g)].map((x) => x[1]!));
    const pairs: Array<[string, string]> = [
      ['setup_standards', 'SetupStandards'], ['create_sheet', 'CreateSheet'], ['insert_library_block', 'InsertLibraryBlock'], ['layer_state', 'LayerState'],
    ];
    for (const [tool, fn] of pairs) {
      const keys = new Set(Object.keys(schemaOf(tool).shape));
      for (const k of reads(fn)) assert.ok(keys.has(k), `${tool}: plugin reads '${k}' that the schema does not offer`);
    }
    // handles of an overwrite go through ApprovedOverwrites (p.Handles("handles")): offered on both overwrite tools
    for (const t of ['setup_standards', 'layer_state']) assert.ok('handles' in schemaOf(t).shape, t);
  });

  it('the schemas accept the calls of the checklist and refuse a wrong paper / action', () => {
    assert.ok(schemaOf('create_sheet').safeParse({ paper: 'A1', orientation: 'portrait', scale: '1:50', fields: { project: 'Nhà phố', sheet_no: 'KT-01' } }).success);
    assert.ok(!schemaOf('create_sheet').safeParse({ paper: 'A5' }).success);
    // an unknown title field is dropped by zod (the plugin also refuses one it receives)
    assert.deepEqual((schemaOf('create_sheet').parse({ fields: { project: 'A', tenant: 'x' } }) as { fields: object }).fields, { project: 'A' });
    assert.ok(schemaOf('insert_library_block').safeParse({ name: 'cua_di', positions: [[0, 0], [1000, 0]], width: 800, flip: true }).success);
    assert.ok(schemaOf('insert_library_block').safeParse({}).success, 'no name = list the library');
    assert.ok(schemaOf('layer_state').safeParse({ action: 'list' }).success);
    assert.ok(!schemaOf('layer_state').safeParse({ action: 'rename', name: 'A' }).success);
    assert.ok(schemaOf('setup_standards').safeParse({ scale: 100, standard: 've-kien-truc' }).success);
    assert.ok(!schemaOf('setup_standards').safeParse({ scale: -100 }).success);
  });
});

describe('F2 test & sửa: the gate in mixed batches', () => {
  const noCard: GateState = { userText: 'đồng ý', answersQuestion: true };
  const card: GateState = { userText: '', answersQuestion: true, confirmedByButton: true, cardConfirmed: new Set(['1A2B']) };

  it('an approved overwrite next to an unapproved one is still refused', () => {
    const calls = [
      { tool: 'setup_standards', params: { scale: '1:100', handles: ['1a2b'] } },
      { tool: 'layer_state', params: { action: 'delete', name: 'IN', handles: ['3C4D'] } },
    ];
    const err = confirmationError('batch_execute', { calls }, card);
    assert.ok(err?.startsWith('Confirmation required'), err);
    assert.match(err!, /3C4D/);
    assert.doesNotMatch(err!, /1A2B/);
  });

  it('a batch that creates (no handles) is never gated', () => {
    const calls = [
      { tool: 'setup_standards', params: { scale: '1:100' } },
      { tool: 'create_sheet', params: { paper: 'A3' } },
      { tool: 'insert_library_block', params: { name: 'cua_so', position: [0, 0] } },
      { tool: 'layer_state', params: { action: 'save', name: 'IN' } },
    ];
    assert.equal(confirmationError('batch_execute', { calls }, noCard), undefined);
  });
});
