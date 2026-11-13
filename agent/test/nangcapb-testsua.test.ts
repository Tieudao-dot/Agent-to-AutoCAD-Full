/**
 * Test & sửa Nâng cấp đợt B (13/11/2026, docs/ban-giao/nang-cap-b-test.md), NB-1: stretch_entities without handles
 * changes whatever lies in its crossing window, so the gate cannot count it beforehand.
 *  - the assistant tells the plugin how many objects the turn may still change without asking (max_unasked);
 *  - after the call, the objects it changed count in the turn's 20-object total;
 *  - they are never "created in this turn" (a 3D solid moved by the window stays an existing one for the card rule).
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it } from 'node:test';
import type { SDKUserMessage } from '@anthropic-ai/claude-agent-sdk';
import {
  WINDOW_LIMIT_PARAM, applyUserConfirmation, confirmationError, createdHandles, windowChanged, type GateState,
} from '../src/agent/confirmation.js';
import { AgentSession, type QueryFn } from '../src/agent/session.js';
import type { ToolBridge } from '../src/tools/common.js';

const win = { corner1: [0, 0], corner2: [100, 100], displacement: [50, 0] };
const hs = (k: number, from = 0) => Array.from({ length: k }, (_, i) => `H${i + from}`);
const g = (touched: string[], extra: Partial<GateState> = {}): GateState => ({
  userText: 'kéo giãn', answersQuestion: false, touched: new Set(touched), created: new Set(), ...extra,
});

describe('Test & sửa đợt B: window stretch and the 20-object rule (NB-1)', () => {
  it('a window stretch carries how many objects the turn has left', () => {
    assert.equal(WINDOW_LIMIT_PARAM, 'max_unasked');
    assert.equal(applyUserConfirmation('stretch_entities', win, g([]))[WINDOW_LIMIT_PARAM], 20);
    assert.equal(applyUserConfirmation('stretch_entities', win, g(hs(15)))[WINDOW_LIMIT_PARAM], 5);
    assert.equal(applyUserConfirmation('stretch_entities', win, g(hs(25)))[WINDOW_LIMIT_PARAM], 0);
  });

  it('Claude cannot set the limit itself', () => {
    const sent = applyUserConfirmation('stretch_entities', { ...win, max_unasked: 1000 }, g(hs(18)));
    assert.equal(sent[WINDOW_LIMIT_PARAM], 2);
    // with handles the gate counts them: no limit is sent, and a value Claude put there is dropped
    const named = applyUserConfirmation('stretch_entities', { ...win, handles: ['A1'], max_unasked: 1000 }, g([]));
    assert.equal(WINDOW_LIMIT_PARAM in named, false);
  });

  it('after the user said yes the plugin uses its own limit (20)', () => {
    const yes = g(hs(30), { userText: 'đồng ý', answersQuestion: true });
    assert.equal(WINDOW_LIMIT_PARAM in applyUserConfirmation('stretch_entities', win, yes), false);
    const button = g(hs(30), { confirmedByButton: true });
    assert.equal(WINDOW_LIMIT_PARAM in applyUserConfirmation('stretch_entities', { ...win, max_unasked: 99 }, button), false);
  });

  it('in batch_execute the window calls share what is left', () => {
    const batch = {
      calls: [
        { tool: 'stretch_entities', params: win },
        { tool: 'erase_entities', params: { handles: ['E1'] } },
        { tool: 'stretch_entities', params: { ...win, max_unasked: 50 } },
        { tool: 'stretch_entities', params: { ...win, handles: ['A1'] } },
      ],
    };
    const sent = applyUserConfirmation('batch_execute', batch, g(hs(10))) as { calls: Array<{ params: Record<string, unknown> }> };
    assert.equal(sent.calls[0]!.params[WINDOW_LIMIT_PARAM], 5); // (20 − 10) / 2 window calls
    assert.equal(sent.calls[2]!.params[WINDOW_LIMIT_PARAM], 5);
    assert.equal(WINDOW_LIMIT_PARAM in sent.calls[3]!.params, false);
    assert.equal(WINDOW_LIMIT_PARAM in sent.calls[1]!.params, false);
  });

  it('the objects a window changed are not "created" (3D card rule stays) and are counted', () => {
    const res = { handles: ['2F4A', 'B1'], moved: ['2F4A'], stretched: ['B1'] };
    assert.deepEqual(createdHandles('stretch_entities', win, res), []);
    assert.deepEqual(windowChanged('stretch_entities', win, res), ['2F4A', 'B1']);
    assert.deepEqual(windowChanged('stretch_entities', { ...win, handles: ['2F4A'] }, res), []); // named: counted by the gate
    const batch = { calls: [{ tool: 'stretch_entities', params: win }, { tool: 'draw_line', params: {} }] };
    const br = { results: [{ ok: true, result: res }, { ok: true, result: { handle: 'C9' } }] };
    assert.deepEqual(createdHandles('batch_execute', batch, br), ['C9']);
    assert.deepEqual(windowChanged('batch_execute', batch, br), ['2F4A', 'B1']);
    // a solid the window moved: a later boolean on it still needs the card
    assert.match(confirmationError('solid_union', { handles: ['2F4A', 'C9'] }, g(['2F4A', 'B1'], { created: new Set(['C9']) })) ?? '', /Confirmation required/);
  });

  it('in a session: a window stretch, then more edits, asks once over 20 in total', async () => {
    const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
    const fn = ((params: { prompt: AsyncIterable<SDKUserMessage> }) => {
      const gen = (async function* () {
        for await (const _m of params.prompt) { /* the turn never ends by itself */ }
      })();
      return Object.assign(gen, { interrupt: async () => {}, close: () => {} });
    }) as unknown as QueryFn;
    const s = new AgentSession({ bridge, emit: () => {}, log: () => {}, version: 't', workDir: fs.mkdtempSync(path.join(os.tmpdir(), 'cc-wd-')), queryFn: fn } as never);
    s.sendMessage({ type: 'user_message', text: 'kéo giãn phần bên phải' } as never);
    await new Promise((r) => setTimeout(r, 10));
    assert.equal(s.gate('stretch_entities', win), undefined);
    assert.equal(s.paramsToSend('stretch_entities', win)[WINDOW_LIMIT_PARAM], 20);
    s.noteResult('stretch_entities', win, { ok: true, result: { handles: hs(18), moved: [], stretched: hs(18) } });
    // the second window may change only 2 more
    assert.equal(s.paramsToSend('stretch_entities', win)[WINDOW_LIMIT_PARAM], 2);
    // and erasing 5 others now goes over 20: asks
    assert.match(s.gate('erase_entities', { handles: hs(5, 100) }) ?? '', /Confirmation required/);
    // the same 18 objects again are not counted twice
    assert.equal(s.gate('set_entity_props', { handles: hs(18), color: 1 }), undefined);
  });
});
