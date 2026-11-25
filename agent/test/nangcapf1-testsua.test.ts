/**
 * Test & sửa đợt F1 (docs/ban-giao/nang-cap-f1-test.md): a Full machine in the 3D work mode gets the 3D plan's trimmed
 * schemas, so their notes must not speak of "this machine's plan" (the way out is the mode switch); plus edge cases of
 * the work mode in the prompt and the per-message context line.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { contextPreamble } from '../src/agent/content.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools, PLAN_HIDDEN_PARAMS } from '../src/tools/index.js';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const descOf = (plan: string | undefined, name: string) => buildTools(bridge, undefined, plan).find((t) => t.name === name)!.description;

describe('Test & sửa F1: trimmed tool notes name no plan', () => {
  it('every note of a trimmed tool says "the tools loaded here", never "this machine\'s plan"', () => {
    for (const [name, h] of Object.entries(PLAN_HIDDEN_PARAMS)) {
      assert.doesNotMatch(h.note, /plan/i, name);
      if (h.note) assert.match(h.note, /the tools loaded here/i, name);
    }
  });

  it('a 3D-mode machine (the 3D plan schemas) still sees what to do instead', () => {
    assert.match(descOf('3d', 'volume_schedule'), /show the numbers to the user in the chat/);
    assert.match(descOf('3d', 'check_interference'), /list the pairs in the chat/);
    assert.match(descOf('3d', 'define_block'), /no block attributes/);
    for (const t of ['volume_schedule', 'check_interference', 'define_block', 'insert_block'])
      assert.doesNotMatch(descOf('3d', t), /machine's plan/, t);
  });
});

describe('Test & sửa F1: work mode edge cases', () => {
  it('the prompt of a mode is the plan prompt plus the mode section, without plan / buying talk', () => {
    for (const mode of ['2d', '3d'] as const) {
      const p = systemPromptFor(mode, mode);
      assert.match(p, new RegExp(`# Work mode: ${mode.toUpperCase()}`));
      assert.match(p, /never speak of plans, buying, upgrading or the plugin owner/);
      assert.doesNotMatch(p, /# Service plan/);
    }
    // a mode that does not match the plan is not a mode
    assert.equal(systemPromptFor('2d', '3d'), systemPromptFor('2d'));
  });

  it('the context line of a mode asks to switch, a real 2D / 3D plan keeps its plan line', () => {
    const mode = contextPreamble({ type: 'user_message', id: 'a', text: 'x', context: { service_plan: '3d', work_mode: '3d' } } as never).join('\n');
    assert.match(mode, /Chế độ người dùng chọn: 3D \(máy gói Full\)/);
    assert.match(mode, /never to buy a plan/);
    assert.doesNotMatch(mode, /Gói của máy này/);
    const plan = contextPreamble({ type: 'user_message', id: 'b', text: 'x', context: { service_plan: '3d' } } as never).join('\n');
    assert.match(plan, /Gói của máy này: /);
    assert.doesNotMatch(plan, /Chế độ người dùng chọn/);
    const full = contextPreamble({ type: 'user_message', id: 'c', text: 'x', context: { service_plan: '2d3d' } } as never).join('\n');
    assert.doesNotMatch(full, /Gói của máy này|Chế độ người dùng chọn/);
  });
});
