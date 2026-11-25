/**
 * Test & sửa đợt G2 (docs/ban-giao/nang-cap-g2-test.md): check_drawing / fix_drawing with the deferred tools switched off,
 * the plans and work modes, and the card of fix_drawing.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it } from 'node:test';
import { USER_CONFIRMED_PARAM, applyUserConfirmation } from '../src/agent/confirmation.js';
import { AgentSession } from '../src/agent/session.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { ALWAYS_LOADED_TOOLS, buildTools } from '../src/tools/index.js';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const G2 = ['check_drawing', 'fix_drawing'];

describe('Test & sửa G2: loading', () => {
  it('deferred by default (only their names at chat start), in full when deferring is off', () => {
    for (const t of G2) assert.ok(!ALWAYS_LOADED_TOOLS.includes(t), t);
    const tools = buildTools(bridge);
    for (const t of G2) {
      const x = tools.find((y) => y.name === t)!;
      assert.ok(x, t);
      assert.ok(!(x as any)._meta?.['anthropic/alwaysLoad'], `${t} deferred`);
    }
    const workDir = fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-g2t-'));
    const s = new AgentSession({ emit: () => {}, bridge, version: 't', workDir, log: () => {}, deferTools: false });
    const o = s.buildOptions() as any;
    assert.deepEqual(o.tools, ['Read', 'Skill']);
    s.close();
  });

  it('the hint is in the 2D and Full prompts and the 2D work mode, never on 3D', () => {
    for (const [plan, mode] of [['2d', undefined], ['2d3d', undefined], ['2d', '2d']] as const) assert.match(systemPromptFor(plan, mode), /check_drawing/, `${plan}/${mode}`);
    for (const [plan, mode] of [['3d', undefined], ['3d', '3d']] as const) assert.doesNotMatch(systemPromptFor(plan, mode), /check_drawing|fix_drawing/, `${plan}/${mode}`);
  });
});

describe('Test & sửa G2: the card of fix_drawing', () => {
  const g = { userText: 'đồng ý', answersQuestion: true, cardConfirmed: new Set(['0123456789ABCDEF']) } as never;

  it('only the approved token reaches the plugin; a token for another fix never does', () => {
    const ok = applyUserConfirmation('fix_drawing', { fix: 'duplicates', confirm_handles: ['0123456789abcdef'] }, g);
    assert.deepEqual(ok[USER_CONFIRMED_PARAM], ['0123456789ABCDEF']);
    const other = applyUserConfirmation('fix_drawing', { fix: 'zero_length', confirm_handles: ['FEDCBA9876543210'] }, g);
    assert.ok(!(USER_CONFIRMED_PARAM in other));
    const noCard = applyUserConfirmation('fix_drawing', { fix: 'duplicates', confirm_handles: ['0123456789ABCDEF'] }, { userText: 'ok', answersQuestion: false } as never);
    assert.ok(!(USER_CONFIRMED_PARAM in noCard), 'a typed ok without the card does not count');
  });

  it('check_drawing is a read (allowed in read-only), fix_drawing destructive', () => {
    const tools = new Map(buildTools(bridge).map((t) => [t.name, t as any]));
    assert.equal(tools.get('check_drawing').annotations?.readOnlyHint, true);
    assert.equal(tools.get('fix_drawing').annotations?.destructiveHint, true);
  });
});
