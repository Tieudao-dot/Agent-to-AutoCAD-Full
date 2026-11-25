/**
 * Nâng cấp đợt G1 (docs/ban-giao/nang-cap-g1.md): create_drawing_set — one call, deferred (not always loaded), its card
 * (confirm_handles → user_confirmed only for handles on the approved card), the prompt line.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { F5_CARD_TOOLS, USER_CONFIRMED_PARAM, applyUserConfirmation } from '../src/agent/confirmation.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { ALWAYS_LOADED_TOOLS, buildTools } from '../src/tools/index.js';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };

describe('nâng cấp đợt G1: create_drawing_set', () => {
  it('is one deferred tool of every plan', () => {
    const t = buildTools(bridge).find((x) => x.name === 'create_drawing_set');
    assert.ok(t, 'registered');
    assert.ok(!ALWAYS_LOADED_TOOLS.includes('create_drawing_set'));
    assert.equal((t as any)._meta?.['anthropic/alwaysLoad'], undefined);
    const schema = JSON.stringify(t!.inputSchema);
    for (const k of ['kinds', 'elevations', 'sections', 'plan_source', 'plan_areas', 'paper', 'scale', 'fields', 'pdf', 'confirm_handles']) assert.ok(schema.includes(`"${k}"`), k);
    // a 2D and a 3D machine both get it (each part is checked against its own tool's plan in the plugin)
    assert.ok(buildTools(bridge, ['create_drawing_set', 'get_drawing_info'], '2d').some((x) => x.name === 'create_drawing_set'));
    assert.ok(buildTools(bridge, ['create_drawing_set', 'get_drawing_info'], '3d').some((x) => x.name === 'create_drawing_set'));
  });

  it('replaces the set or the PDF only with the approved card', () => {
    assert.ok(F5_CARD_TOOLS.has('create_drawing_set'));
    const g = { userText: 'đồng ý', answersQuestion: true, cardConfirmed: new Set(['ABCDEF0123456789']) } as never;
    const out = applyUserConfirmation('create_drawing_set', { pdf: true, confirm_handles: ['abcdef0123456789', '1111222233334444'] }, g) as any;
    assert.deepEqual(out[USER_CONFIRMED_PARAM], ['ABCDEF0123456789']);
    assert.equal(out.confirm_handles, undefined);
    const forged = applyUserConfirmation('create_drawing_set', { [USER_CONFIRMED_PARAM]: ['ABCDEF0123456789'] }, { userText: 'ok', answersQuestion: false } as never) as any;
    assert.equal(forged[USER_CONFIRMED_PARAM], undefined);
  });

  it('the prompt names it once, short', () => {
    const p = systemPromptFor('2d3d');
    const lines = p.split('\n').filter((l) => l.includes('create_drawing_set'));
    assert.equal(lines.length, 1);
    assert.ok(lines[0]!.length < 300);
    assert.ok(systemPromptFor('2d').includes('create_drawing_set') && systemPromptFor('3d').includes('create_drawing_set'));
  });
});
