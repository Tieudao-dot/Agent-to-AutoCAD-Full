/**
 * Nâng cấp đợt H4 (docs/ban-giao/nang-cap-h4.md): company_standard — schema, loading, cards, the company parameter of the
 * three tools that use it, the prompt line.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { F5_CARD_TOOLS } from '../src/agent/confirmation.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { ALWAYS_LOADED_TOOLS, buildTools } from '../src/tools/index.js';
import { COMPANY_ACTIONS } from '../src/tools/companyStandard.js';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const tools = new Map(buildTools(bridge).map((t) => [t.name, t as any]));

describe('H4: company_standard', () => {
  const cs = tools.get('company_standard');

  it('one deferred tool with four actions; replacing / deleting asks for the card', () => {
    assert.ok(cs);
    assert.deepEqual([...COMPANY_ACTIONS], ['learn', 'list', 'show', 'delete']);
    assert.ok(!ALWAYS_LOADED_TOOLS.includes('company_standard'));
    assert.ok(!cs._meta?.['anthropic/alwaysLoad']);
    assert.ok(F5_CARD_TOOLS.has('company_standard'));
    assert.ok(cs.inputSchema.action.safeParse('learn').success);
    assert.ok(!cs.inputSchema.action.safeParse('apply').success);
    assert.match(cs.description, /only read/);
    assert.ok(cs.description.length < 1000, 'short description');
  });

  it('setup_standards, check_drawing and create_sheet take company; create_sheet attributes', () => {
    for (const n of ['setup_standards', 'check_drawing', 'create_sheet']) assert.ok(tools.get(n).inputSchema.company?.safeParse('Công ty ABC').success, n);
    assert.ok(tools.get('create_sheet').inputSchema.attributes.safeParse({ TEN_DA: 'Nhà phố' }).success);
    // Test & sửa H4: fix_drawing takes company only so the plugin refuses its standard groups (it never applies company styles)
    assert.match(tools.get('fix_drawing').inputSchema.company.description, /not repaired here/);
  });

  it('one line in the 2D and Full prompts, none on 3D', () => {
    for (const p of ['2d', '2d3d'] as const) assert.equal(systemPromptFor(p).split('\n').filter((l) => l.includes('company_standard')).length, 1, p);
    assert.ok(!systemPromptFor('3d').includes('company_standard'));
  });
});
