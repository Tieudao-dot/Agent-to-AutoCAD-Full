/**
 * Test & sửa đợt F2 (25/11/2026, docs/ban-giao/nang-cap-f2-test.md): cross cases the F2 tests did not cover —
 * partly approved cards, the UCS wording of create_sheet, the work modes.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { confirmationError, type GateState } from '../src/agent/confirmation.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import { standards2dTools } from '../src/tools/standards2d.js';
import type { ToolBridge } from '../src/tools/common.js';
import { z } from 'zod';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };

describe('Test & sửa đợt F2', () => {
  it('a card that approved only some of the handles does not let the others through', () => {
    const card: GateState = { userText: '', answersQuestion: true, confirmedByButton: true, cardConfirmed: new Set(['1A']) };
    const err = confirmationError('setup_standards', { scale: '1:100', handles: ['1a', '2b'] }, card);
    assert.ok(err?.startsWith('Confirmation required'), err);
    assert.match(err!, /2B/);
    assert.doesNotMatch(err!, /\b1A\b/);
    assert.equal(confirmationError('setup_standards', { scale: '1:100', handles: ['1a'] }, card), undefined);
  });

  it('create_sheet takes its view centre in the current UCS like the other model points', () => {
    const t = standards2dTools(bridge).find((x) => x.name === 'create_sheet')!;
    const schema = JSON.stringify(z.toJSONSchema(z.object(t.inputSchema as z.ZodRawShape)));
    assert.match(schema, /current UCS/);
  });

  it('the 3D work mode of a Full machine never names the 2D-only F2 tools, the 2D mode does', () => {
    const three = systemPromptFor('3d', '3d');
    for (const n of ['setup_standards', 'create_sheet']) assert.ok(!three.includes(n), n);
    const two = systemPromptFor('2d', '2d');
    assert.ok(two.includes('setup_standards'));
  });
});
