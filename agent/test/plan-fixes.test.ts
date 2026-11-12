/**
 * Service plan fixes after the independent review (12/11/2026): KT-02 block attributes, KT-03 "−" / "+" skill notes
 * per plan, KT-35 capture_view 3D parameters, KT-36 skill plan lines.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it } from 'node:test';
import type { SDKMessage, SDKUserMessage } from '@anthropic-ai/claude-agent-sdk';
import { AgentSession, type QueryFn } from '../src/agent/session.js';
import { skillPlan } from '../src/agent/skills.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';
import { interactionTools, skillMarkdown } from '../src/tools/interaction.js';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const shapeOf = (plan: string | undefined, name: string) =>
  Object.keys(buildTools(bridge, undefined, plan).find((t) => t.name === name)!.inputSchema as object);
const descOf = (plan: string | undefined, name: string) => buildTools(bridge, undefined, plan).find((t) => t.name === name)!.description;

function library(skills: Record<string, string | undefined>): string {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-planfix-'));
  for (const [n, plan] of Object.entries(skills)) {
    fs.mkdirSync(path.join(dir, n));
    fs.writeFileSync(path.join(dir, n, 'SKILL.md'), `---\nname: ${n}\ndescription: Skill ${n} dùng khi vẽ.\n${plan ? `plan: ${plan}\n` : ''}---\n# ${n}\n`);
  }
  return dir;
}

describe('KT-02: block attributes need 2D', () => {
  it('a 3D machine does not see the attribute parameters of define_block / insert_block', () => {
    for (const t of ['define_block', 'insert_block']) {
      assert.ok(!shapeOf('3d', t).includes('attributes'), t);
      assert.match(descOf('3d', t), /no block attributes/);
      assert.doesNotMatch(descOf('3d', t), /\{TAG: value\}|Optional attribute definitions/);
      for (const plan of ['2d', '2d3d', undefined]) assert.ok(shapeOf(plan, t).includes('attributes'), `${t} ${plan}`);
    }
  });

  it('the 3D system prompt says block attributes are not included', () => {
    assert.match(systemPromptFor('3d'), /Not included: text, block attributes/);
    assert.match(systemPromptFor('3d'), /blocks without attributes/);
  });
});

describe('KT-35: capture_view without 3D parameters on a 2D machine', () => {
  it('drops view / direction / visual_style and the 3D sentence on 2D only', () => {
    const two = shapeOf('2d', 'capture_view');
    for (const k of ['view', 'direction', 'visual_style']) assert.ok(!two.includes(k), k);
    assert.ok(two.includes('handles') && two.includes('extents'));
    assert.doesNotMatch(descOf('2d', 'capture_view'), /3D:/);
    for (const plan of ['3d', '2d3d', undefined]) assert.ok(shapeOf(plan, 'capture_view').includes('visual_style'), String(plan));
  });
});

describe('KT-36: skill plan lines', () => {
  it('a "# comment" after the plan value is ignored', () => {
    const dir = library({});
    fs.mkdirSync(path.join(dir, 'chuan-x'));
    fs.writeFileSync(path.join(dir, 'chuan-x', 'SKILL.md'), '---\nname: chuan-x\ndescription: d\nplan: 2d # chỉ cho máy 2D\n---\nx\n');
    assert.equal(skillPlan(dir, 'chuan-x'), '2d');
    fs.writeFileSync(path.join(dir, 'chuan-x', 'SKILL.md'), '---\nname: chuan-x\ndescription: d\nplan: "3d"   #3D\n---\nx\n');
    assert.equal(skillPlan(dir, 'chuan-x'), '3d');
  });

  it('a skill drafted with propose_skill carries the plan of the machine (none on Full)', () => {
    assert.match(skillMarkdown('a-b', 'mô tả đủ dài cho skill này', 'x', '2d'), /^---\nname: a-b\ndescription: ".*"\nplan: 2d\n---/);
    assert.match(skillMarkdown('a-b', 'mô tả đủ dài cho skill này', 'x', '3d'), /\nplan: 3d\n---/);
    assert.doesNotMatch(skillMarkdown('a-b', 'mô tả đủ dài cho skill này', 'x', '2d3d'), /plan:/);
    assert.doesNotMatch(skillMarkdown('a-b', 'mô tả đủ dài cho skill này', 'x'), /plan:/);
  });

  it('propose_skill sends the draft with the plan line', async () => {
    const sent: any[] = [];
    const t = interactionTools((m) => sent.push(m), '3d').find((x) => x.name === 'propose_skill')!;
    await t.handler({ name: 'chuan-3d', description: 'Chuẩn dựng khối 3D của công ty', body: 'Nội dung' }, {});
    const draft = sent.find((m) => m.type === 'skill_draft');
    assert.match(draft.content, /\nplan: 3d\n/);
  });
});

describe('KT-03: the "−" / "+" skill notes follow the plan', () => {
  const init = { type: 'system', subtype: 'init', session_id: 's1', model: 'm', apiKeySource: 'oauth', tools: [], skills: [] } as unknown as SDKMessage;
  const result = { type: 'result', subtype: 'success', is_error: false, session_id: 's1' } as unknown as SDKMessage;
  const tick = (ms = 20) => new Promise((r) => setTimeout(r, ms));

  it('a 3D skill added on 2D is not "added"; a 2D skill the new 3D plan drops is "removed"', async () => {
    const texts: string[] = [];
    const fn = ((params: { prompt: AsyncIterable<SDKUserMessage>; options: any }) => {
      const gen = (async function* () {
        for await (const m of params.prompt) {
          texts.push(String(m.message.content));
          yield init;
          yield result;
        }
      })();
      return Object.assign(gen, { interrupt: async () => {}, close: () => {} });
    }) as unknown as QueryFn;
    const dir = library({ 've-2d': '2d', 've-3d': '3d', 'chung': undefined });
    const s = new AgentSession({ bridge, emit: () => {}, log: () => {}, version: 't', workDir: fs.mkdtempSync(path.join(os.tmpdir(), 'cc-wd-')), skillsDir: dir, queryFn: fn } as never);
    const send = (plan: string, skills: string[]) =>
      s.sendMessage({ type: 'user_message', text: 'vẽ', skills, context: { service_plan: plan } } as never);

    send('2d', ['ve-2d', 'chung']);
    await tick();
    send('2d', ['ve-2d', 'chung', 've-3d']); // "+" a 3D skill on a 2D machine: not loaded, not announced
    await tick();
    assert.doesNotMatch(texts.at(-1)!, /vừa thêm skill/);
    send('2d3d', ['ve-2d', 'chung', 've-3d']); // the owner opens Full: now it is really added
    await tick();
    assert.match(texts.at(-1)!, /vừa thêm skill vào đoạn chat này: "ve-3d"/);
    send('3d', ['ve-2d', 'chung', 've-3d']); // Full → 3D: the 2D skill is dropped and Claude is told so
    await tick();
    assert.match(texts.at(-1)!, /đã bỏ skill khỏi đoạn chat này: "ve-2d"/);
    assert.doesNotMatch(texts.at(-1)!, /vừa thêm skill/);
  });
});
