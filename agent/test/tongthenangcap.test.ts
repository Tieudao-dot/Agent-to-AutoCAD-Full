/**
 * Test tổng thể nâng cấp A → E (docs/ban-giao/test-tong-the-nang-cap.md): where the five phases meet the plans.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const scope = (plan: '2d' | '3d') => {
  const p = systemPromptFor(plan);
  const s = p.slice(p.indexOf("# This machine's service plan"));
  const i = s.indexOf('Not included:');
  return { included: s.slice(0, i), excluded: s.slice(i) };
};

describe('TT2-2: the plan scope names the tools of phases A → E', () => {
  it('3D plan: the new 3D tools are included, the new 2D tools are named as not included', () => {
    const { included, excluded } = scope('3d');
    for (const w of ['columns', 'beams', 'stairs', 'roofs', 'railings', 'PRESSPULL', 'STL export', 'groups', 'match properties'])
      assert.ok(included.includes(w), `3D includes ${w}`);
    assert.match(included, /volume and clash numbers without a table/);
    for (const w of ['stretch', 'break', 'lengthen', 'divide / measure', 'plan dimensions', 'block / area schedules', 'find & replace of text'])
      assert.ok(excluded.includes(w), `3D excludes ${w}`);
  });

  it('2D plan: the new 2D tools are included, 3D (beams, stairs, roofs...) is not', () => {
    const { included, excluded } = scope('2d');
    for (const w of ['groups', 'match properties', 'tables and schedules', 'find & replace of text', 'stretch', 'break', 'lengthen', 'divide / measure'])
      assert.ok(included.includes(w), `2D includes ${w}`);
    for (const w of ['beams', 'stairs', 'roofs', 'railings', 'STL']) assert.ok(excluded.includes(w), `2D excludes ${w}`);
  });

  it('Full has no scope list (every tool)', () => {
    assert.match(systemPromptFor('2d3d'), /Every tool is included\./);
  });
});

describe('TT2-1: build_roof says where it sits on a roof slab', () => {
  it('the description names the roof slab rule', () => {
    const roof = buildTools(bridge).find((t) => t.name === 'build_roof')!;
    assert.match(roof.description, /a roof slab on A-SLAB-3D lying on them: its top/);
  });
});
