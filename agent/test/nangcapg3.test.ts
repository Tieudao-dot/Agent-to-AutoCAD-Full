/**
 * Nâng cấp đợt G3 (docs/ban-giao/nang-cap-g3.md): cost_estimate — deferred, prices only from the user (prompt and
 * description), the Excel card, the drawn table hidden on the 3D plan.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { F5_CARD_TOOLS, USER_CONFIRMED_PARAM, applyUserConfirmation } from '../src/agent/confirmation.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { ALWAYS_LOADED_TOOLS, buildTools } from '../src/tools/index.js';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };

describe('nâng cấp đợt G3: cost_estimate', () => {
  it('is deferred and says prices come only from the user', () => {
    const t = buildTools(bridge).find((x) => x.name === 'cost_estimate');
    assert.ok(t);
    assert.ok(!ALWAYS_LOADED_TOOLS.includes('cost_estimate'));
    assert.equal((t as any)._meta?.['anthropic/alwaysLoad'], undefined);
    assert.match(t!.description, /Never invent/);
    assert.match(t!.description, /missing_prices/);
    const lines = systemPromptFor('2d3d').split('\n').filter((l) => l.includes('cost_estimate'));
    assert.equal(lines.length, 1);
    assert.match(lines[0]!, /never invent/);
  });

  it('the 3D plan gets numbers and Excel, no drawn table; 2D and Full get the table', () => {
    const schema = (plan: string) => JSON.stringify(buildTools(bridge, ['cost_estimate'], plan).find((x) => x.name === 'cost_estimate')!.inputSchema);
    assert.ok(!schema('3d').includes('"table_position"'));
    assert.ok(schema('3d').includes('"excel"'));
    assert.ok(schema('2d').includes('"table_position"'));
    assert.ok(schema('2d3d').includes('"table_position"'));
  });

  it('overwrites an Excel file only with the approved card', () => {
    assert.ok(F5_CARD_TOOLS.has('cost_estimate'));
    const g = { userText: 'đồng ý', answersQuestion: true, cardConfirmed: new Set(['0123456789ABCDEF']) } as never;
    const out = applyUserConfirmation('cost_estimate', { quantities: 'volume', excel: true, confirm_handles: ['0123456789abcdef'] }, g) as any;
    assert.deepEqual(out[USER_CONFIRMED_PARAM], ['0123456789ABCDEF']);
  });
});
