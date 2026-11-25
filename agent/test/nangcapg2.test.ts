/**
 * Nâng cấp đợt G2 (docs/ban-giao/nang-cap-g2.md): check_drawing (read only) and fix_drawing (after the card), in the 2D
 * hints, outside the always-loaded tools; the card's token reaches the plugin only when it is on the approved card.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { F5_CARD_TOOLS, USER_CONFIRMED_PARAM, applyUserConfirmation } from '../src/agent/confirmation.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { CHECK_GROUPS, FIX_GROUPS } from '../src/tools/drawingCheck.js';
import { ALWAYS_LOADED_TOOLS, buildTools } from '../src/tools/index.js';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };

describe('nâng cấp đợt G2: soát bản vẽ', () => {
  const tools = buildTools(bridge);
  const check = tools.find((t) => t.name === 'check_drawing')!;
  const fix = tools.find((t) => t.name === 'fix_drawing')!;

  it('declares both tools, the check read only, the fix destructive', () => {
    assert.ok(check && fix);
    assert.equal((check as any).annotations?.readOnlyHint, true);
    assert.equal((fix as any).annotations?.destructiveHint, true);
    const shape = fix.inputSchema as Record<string, any>;
    assert.ok(shape.fix && shape.layer && shape.handles && shape.confirm_handles);
    assert.ok((check.inputSchema as Record<string, unknown>).excel);
  });

  it('repairs only groups that are checked', () => {
    for (const f of FIX_GROUPS) assert.ok((CHECK_GROUPS as readonly string[]).includes(f), f);
    assert.equal(FIX_GROUPS.length, 9);
    assert.equal(CHECK_GROUPS.length, 22);
  });

  it('is not always loaded (found with ToolSearch when needed)', () => {
    assert.ok(!ALWAYS_LOADED_TOOLS.includes('check_drawing'));
    assert.ok(!ALWAYS_LOADED_TOOLS.includes('fix_drawing'));
    const t = buildTools(bridge, undefined, '2d').find((x) => x.name === 'check_drawing') as any;
    assert.notEqual(t?._meta?.['anthropic/alwaysLoad'], true);
  });

  it('passes the card token on only after Đồng ý on that card (also in a batch)', () => {
    assert.ok(F5_CARD_TOOLS.has('fix_drawing') && F5_CARD_TOOLS.has('check_drawing'));
    const g = { userText: 'đồng ý', answersQuestion: true, cardConfirmed: new Set(['0123456789ABCDEF']) } as never;
    const ok = applyUserConfirmation('fix_drawing', { fix: 'duplicates', confirm_handles: ['0123456789abcdef'] }, g) as any;
    assert.deepEqual(ok[USER_CONFIRMED_PARAM], ['0123456789ABCDEF']);
    assert.equal(ok.confirm_handles, undefined);
    const guessed = applyUserConfirmation('fix_drawing', { fix: 'duplicates', confirm_handles: ['FFFFFFFFFFFFFFFF'] }, g) as any;
    assert.equal(guessed[USER_CONFIRMED_PARAM], undefined);
    const forged = applyUserConfirmation('fix_drawing', { fix: 'duplicates', [USER_CONFIRMED_PARAM]: ['0123456789ABCDEF'] }, { userText: 'x', answersQuestion: false } as never) as any;
    assert.equal(forged[USER_CONFIRMED_PARAM], undefined);
    const b = applyUserConfirmation('batch_execute', { calls: [{ tool: 'fix_drawing', params: { fix: 'zero_length', confirm_handles: ['0123456789abcdef'] } }] }, g) as any;
    assert.deepEqual(b.calls[0].params[USER_CONFIRMED_PARAM], ['0123456789ABCDEF']);
  });

  it('is in the 2D hints only (one short line)', () => {
    const line = (p: string) => p.split('\n').filter((l) => l.includes('check_drawing'));
    assert.equal(line(systemPromptFor('2d')).length, 1);
    assert.equal(line(systemPromptFor('2d3d')).length, 1);
    assert.equal(line(systemPromptFor('3d')).length, 0);
    assert.ok(line(systemPromptFor('2d'))[0].length < 200);
  });
});
