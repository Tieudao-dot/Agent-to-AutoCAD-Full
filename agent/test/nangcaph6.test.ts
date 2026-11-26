/**
 * Nâng cấp đợt H6 (docs/ban-giao/nang-cap-h6.md): terrain — deferred, no card (only the drawing changes), one prompt line
 * on 2D / Full, none on 3D, the standard numbers (waiting for approval) not in its description, a slow tool.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { F5_CARD_TOOLS } from '../src/agent/confirmation.js';
import { timeoutFor } from '../src/ipc/rpc.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import { ALWAYS_LOADED_TOOLS, buildTools } from '../src/tools/index.js';

const bridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} } as any;

describe('H6: terrain', () => {
  it('is deferred, not a card tool, one prompt line on 2D / Full, none on 3D', () => {
    const t = buildTools(bridge).find((x) => x.name === 'terrain') as any;
    assert.ok(t);
    assert.ok(!ALWAYS_LOADED_TOOLS.includes('terrain'));
    assert.ok(!F5_CARD_TOOLS.has('terrain'));
    for (const a of ['import_points', 'contours', 'stations', 'profile', 'cross_sections']) assert.ok(t.inputSchema.action.safeParse(a).success);
    assert.ok(!t.inputSchema.action.safeParse('volumes').success);
    for (const p of ['2d', '2d3d'] as const) assert.equal(systemPromptFor(p).split('\n').filter((l) => l.includes('terrain')).length, 1);
    assert.ok(!systemPromptFor('3d').includes('terrain'));
  });

  it('origin, design and the alignment handle are checked', () => {
    const t = buildTools(bridge).find((x) => x.name === 'terrain') as any;
    assert.ok(t.inputSchema.origin.safeParse('auto').success);
    assert.ok(t.inputSchema.origin.safeParse([1234000, 567000]).success);
    assert.ok(!t.inputSchema.origin.safeParse('somewhere').success);
    assert.ok(t.inputSchema.design.safeParse([[0, 10], [100, 12]]).success);
    assert.ok(!t.inputSchema.design.safeParse([[0, 10]]).success);
    assert.ok(t.inputSchema.alignment_handle.safeParse('2F4A').success);
    assert.ok(!t.inputSchema.alignment_handle.safeParse('tim tuyến').success);
  });

  it('the description says it is a drawing aid and carries no standard number', () => {
    const t = buildTools(bridge).find((x) => x.name === 'terrain') as any;
    assert.match(t.description, /engineer checks/);
    assert.match(t.description, /no cut \/ fill/);
    for (const n of ['1:1000', '1:100', '1:200', ' 20 m', '15 m', '0.5 m']) assert.ok(!t.description.includes(n), n);
  });

  it('is a slow tool (10 000 points, contours)', () => {
    assert.equal(timeoutFor('terrain', { action: 'contours' }, 30_000), 120_000);
  });
});
