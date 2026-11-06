/**
 * Sidecar side of the palette features: message content (selection, context,
 * images, edited/confirm flags), protocol parsing, tool activity summaries,
 * model/effort switching, per-drawing session switching, usage, rate limits and
 * the confirmation card tool.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it } from 'node:test';
import type { SDKMessage, SDKUserMessage } from '@anthropic-ai/claude-agent-sdk';
import { contextPreamble, summarizeResult, toSdkUserMessage } from '../src/agent/content.js';
import { AgentSession, type QueryFn } from '../src/agent/session.js';
import { parsePluginMessage, type SidecarToPlugin } from '../src/ipc/protocol.js';
import { interactionTools } from '../src/tools/interaction.js';

describe('message content', () => {
  it('prefixes drawing, standard, scale, selection and flags', () => {
    const lines = contextPreamble({
      type: 'user_message',
      text: 'đổi mấy cái này sang layer A-WALL',
      drawing: 'nha.dwg',
      context: { standard: 've-kien-truc', scale: '1:50' },
      attachments: { selection: { handles: ['2F4A', '2F4B'], summary: '2 LINE' } },
      edited: true,
      confirm: 'yes',
    });
    const all = lines.join('\n');
    assert.match(all, /Bản vẽ đang mở: nha\.dwg/);
    assert.match(all, /skill "ve-kien-truc"/);
    assert.match(all, /1:50/);
    assert.match(all, /đang chọn: 2 \(2 LINE\).*2F4A, 2F4B/);
    assert.match(all, /hoàn tác/);
    assert.match(all, /Đồng ý/);
  });

  it('caps very large selections', () => {
    const handles = Array.from({ length: 900 }, (_, i) => (i + 1).toString(16));
    const line = contextPreamble({ type: 'user_message', text: 'x', attachments: { selection: { handles } } })[0]!;
    assert.match(line, /\(\+400\)/);
  });

  it('plain text without attachments, image blocks before text otherwise', () => {
    const plain = toSdkUserMessage({ type: 'user_message', text: 'chào' });
    assert.equal(plain.message.content, 'chào');
    const withImg = toSdkUserMessage({
      type: 'user_message',
      text: 'vẽ theo phác thảo',
      attachments: { images: [{ media_type: 'image/png', data: 'iVBORw0KGgo=' }] },
    });
    const blocks = withImg.message.content as Array<{ type: string; source?: { media_type: string } }>;
    assert.equal(blocks[0]!.type, 'image');
    assert.equal(blocks[0]!.source!.media_type, 'image/png');
    assert.equal(blocks.at(-1)!.type, 'text');
  });

  it('summarizes tool results for the activity log', () => {
    assert.deepEqual(summarizeResult('draw_line', { handles: ['A1'] }), { summary: '1 đối tượng', handles: ['A1'] });
    assert.equal(summarizeResult('batch_execute', { ok_count: 30, error_count: 2, handles: ['A', 'B'] }).summary, '30 lệnh OK, 2 lỗi, 2 đối tượng');
    assert.equal(summarizeResult('erase_entities', { erased: 57, handles: [] }).summary, 'xóa 57');
    assert.equal(summarizeResult('measure_area', { area_m2: 21.85 }).summary, '21.85 m²');
  });
});

describe('protocol: palette messages', () => {
  it('parses attachments, context and flags; drops malformed images', () => {
    const m = parsePluginMessage({
      type: 'user_message',
      text: 'x',
      attachments: {
        selection: { handles: ['1A', 2, '1B'], count: 2 },
        images: [{ media_type: 'image/png', data: 'AAAA' }, { media_type: 'text/html', data: 'x' }],
      },
      context: { standard: 've-mep', scale: '' },
      edited: true,
      confirm: 'yes',
    });
    assert.deepEqual(m, {
      type: 'user_message',
      text: 'x',
      attachments: { selection: { handles: ['1A', '1B'], count: 2 }, images: [{ media_type: 'image/png', data: 'AAAA' }] },
      context: { standard: 've-mep' },
      edited: true,
      confirm: 'yes',
    });
  });

  it('parses set_options and switch_session', () => {
    assert.deepEqual(parsePluginMessage({ type: 'set_options', model: 'claude-sonnet-5-5', effort: 'high' }), {
      type: 'set_options', model: 'claude-sonnet-5-5', effort: 'high',
    });
    assert.deepEqual(parsePluginMessage({ type: 'switch_session', session_id: 'abc' }), { type: 'switch_session', session_id: 'abc' });
    assert.deepEqual(parsePluginMessage({ type: 'switch_session' }), { type: 'switch_session' });
  });
});

// ---------------------------------------------------------------- session with a fake query

interface FakeCalls {
  options: any[];
  setModel: string[];
  flags: unknown[];
  prompts: SDKUserMessage[];
}

function fakeQuery(script: (input: AsyncIterable<SDKUserMessage>, push: (m: SDKMessage) => void, call: number) => Promise<void>) {
  const calls: FakeCalls = { options: [], setModel: [], flags: [], prompts: [] };
  let n = 0;
  const fn = ((params: { prompt: AsyncIterable<SDKUserMessage>; options: any }) => {
    calls.options.push(params.options);
    const call = n++;
    const queue: SDKMessage[] = [];
    let wake: (() => void) | undefined;
    let done = false;
    let failure: unknown;
    const tap = (async function* () {
      for await (const m of params.prompt) {
        calls.prompts.push(m);
        yield m;
      }
    })();
    script(tap, (m) => { queue.push(m); wake?.(); }, call)
      .catch((e) => { failure = e; })
      .finally(() => { done = true; wake?.(); });
    const gen = (async function* () {
      while (true) {
        if (queue.length) { yield queue.shift()!; continue; }
        if (done) { if (failure) throw failure; return; }
        await new Promise<void>((r) => (wake = r));
        wake = undefined;
      }
    })();
    return Object.assign(gen, {
      interrupt: async () => {},
      close: () => {},
      setModel: async (m: string) => { calls.setModel.push(m); },
      applyFlagSettings: async (f: unknown) => { calls.flags.push(f); },
    });
  }) as unknown as QueryFn;
  return { fn, calls };
}

const init = (id: string) => ({ type: 'system', subtype: 'init', session_id: id, model: 'claude-opus-5-5', apiKeySource: 'oauth', tools: [], skills: [] }) as unknown as SDKMessage;
const result = { type: 'result', subtype: 'success', is_error: false, session_id: 's', total_cost_usd: 0.042, usage: { input_tokens: 100, cache_read_input_tokens: 900, cache_creation_input_tokens: 0, output_tokens: 50 } } as unknown as SDKMessage;
const tick = (ms = 20) => new Promise((r) => setTimeout(r, ms));

function session(fn: QueryFn) {
  const events: SidecarToPlugin[] = [];
  const s = new AgentSession({
    emit: (m) => events.push(m),
    bridge: { call: async (id) => ({ id, ok: true, result: {} }), status: () => {} },
    version: 'test',
    workDir: fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-pal-')),
    log: () => {},
    queryFn: fn,
  });
  return { s, events };
}

describe('AgentSession palette features', () => {
  it('reports session id, usage and rate limits', async () => {
    const { fn } = fakeQuery(async (input, push) => {
      for await (const _ of input) {
        push(init('sess-1'));
        push({ type: 'rate_limit_event', rate_limit_info: { status: 'allowed_warning', utilization: 0.82, rateLimitType: 'five_hour', resetsAt: 1700000000 }, uuid: 'u', session_id: 'sess-1' } as unknown as SDKMessage);
        push(result);
      }
    });
    const { s, events } = session(fn);
    s.send('x');
    await tick();
    assert.deepEqual(events.find((e) => e.type === 'session_info'), { type: 'session_info', session_id: 'sess-1', model: 'claude-opus-5-5' });
    assert.deepEqual(events.find((e) => e.type === 'rate_limit'), { type: 'rate_limit', status: 'allowed_warning', utilization: 0.82, resets_at: 1700000000, limit_type: 'five_hour' });
    assert.deepEqual(events.find((e) => e.type === 'turn_end'), { type: 'turn_end', usage: { input_tokens: 1000, output_tokens: 50, session_cost_usd: 0.042 } });
    s.close();
  });

  it('applies model and effort live and remembers them for the next session', async () => {
    const { fn, calls } = fakeQuery(async (input, push) => { for await (const _ of input) push(result); });
    const { s, events } = session(fn);
    s.send('x');
    await tick();
    await s.setOptions({ model: 'claude-sonnet-5-5', effort: 'high' });
    assert.deepEqual(calls.setModel, ['claude-sonnet-5-5']);
    assert.deepEqual(calls.flags, [{ effortLevel: 'high' }]);
    assert.deepEqual(events.find((e) => e.type === 'options'), { type: 'options', model: 'claude-sonnet-5-5', effort: 'high' });
    s.reset();
    s.send('y');
    await tick();
    assert.equal(calls.options.at(-1).model, 'claude-sonnet-5-5');
    assert.equal(calls.options.at(-1).effort, 'high');
    s.close();
  });

  it('switchTo resumes the drawing session on the next message', async () => {
    const { fn, calls } = fakeQuery(async (input, push) => { for await (const _ of input) { push(init('new')); push(result); } });
    const { s } = session(fn);
    s.switchTo('old-session-of-drawing-B');
    s.send('tiếp tục');
    await tick();
    assert.equal(calls.options[0].resume, 'old-session-of-drawing-B');
    s.switchTo(undefined);
    s.send('mới');
    await tick();
    assert.equal(calls.options[1].resume, undefined);
    s.close();
  });

  it('falls back to a fresh session and re-sends when resume fails', async () => {
    const { fn, calls } = fakeQuery(async (input, push, call) => {
      for await (const _ of input) {
        if (call === 0) throw new Error('No conversation found with session ID');
        push(init('fresh'));
        push(result);
      }
    });
    const { s, events } = session(fn);
    s.switchTo('gone');
    s.send('vẽ lại');
    await tick(60);
    assert.equal(calls.options.length, 2);
    assert.equal(calls.options[1].resume, undefined);
    assert.equal(calls.prompts.length, 2);
    assert.ok(events.some((e) => e.type === 'agent_error' && /cuộc chat mới/.test(e.message)));
    assert.equal(events.filter((e) => e.type === 'turn_end').length, 1);
    s.close();
  });

  it('marks a turn started by the Đồng ý button as confirmed', async () => {
    const { fn } = fakeQuery(async (input, push) => { for await (const _ of input) push(result); });
    const { s } = session(fn);
    s.sendMessage({ type: 'user_message', text: 'Đồng ý', confirm: 'yes' });
    assert.equal(s.lastUserConfirmed, true);
    await tick();
    s.sendMessage({ type: 'user_message', text: 'vẽ tiếp' });
    assert.equal(s.lastUserConfirmed, false);
    s.close();
  });
});

describe('AgentSession robustness', () => {
  it('a new message abandons a turn the plugin already ended (no stuck palette)', async () => {
    let n = 0;
    const { fn, calls } = fakeQuery(async (input, push) => {
      for await (const _ of input) {
        n++;
        if (n === 1) { await tick(60); push(result); } // slow first turn; its result arrives late
        else push(result);
      }
    });
    const { s, events } = session(fn);
    s.send('một việc rất lâu');
    await tick(10);
    s.send('việc mới');
    await tick(150);
    assert.equal(calls.prompts.length, 2);
    // only the second turn produces a turn_end; the late result of the first is ignored
    assert.equal(events.filter((e) => e.type === 'turn_end').length, 1);
    assert.equal(events.filter((e) => e.type === 'agent_error').length, 0);
    assert.equal(s.busy, false);
    s.close();
  });

  it('"default" resets model and effort', async () => {
    const { fn, calls } = fakeQuery(async (input, push) => { for await (const _ of input) push(result); });
    const events: SidecarToPlugin[] = [];
    const s = new AgentSession({
      emit: (m) => events.push(m),
      bridge: { call: async (id) => ({ id, ok: true, result: {} }), status: () => {} },
      version: 'test', workDir: fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-def-')), log: () => {}, queryFn: fn,
      model: 'claude-opus-5-5',
    });
    s.send('x');
    await tick();
    await s.setOptions({ model: 'claude-haiku-5-5', effort: 'low' });
    await s.setOptions({ model: 'default', effort: 'default' });
    assert.deepEqual(calls.setModel, ['claude-haiku-5-5', 'claude-opus-5-5']);
    assert.deepEqual(calls.flags, [{ effortLevel: 'low' }, { effortLevel: null }]);
    s.close();
  });
});

describe('skills added from the palette', () => {
  function skillsDir() {
    const d = fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-sk-'));
    for (const n of ['ve-kien-truc', 've-ket-cau', 've-mep']) {
      fs.mkdirSync(path.join(d, n));
      fs.writeFileSync(path.join(d, n, 'SKILL.md'), `---\nname: ${n}\ndescription: test skill ${n} for the palette tests\n---\nbody`);
    }
    fs.writeFileSync(path.join(d, 'skills.json'), JSON.stringify({ version: 1, skills: [{ name: 've-mep', enabled: false }, { name: 've-ket-cau', enabled: true }] }));
    return d;
  }

  it('loads only enabled skills from skills.json', async () => {
    const { fn, calls } = fakeQuery(async (input, push) => { for await (const _ of input) push(result); });
    const events: SidecarToPlugin[] = [];
    const s = new AgentSession({
      emit: (m) => events.push(m),
      bridge: { call: async (id) => ({ id, ok: true, result: {} }), status: () => {} },
      version: 'test', workDir: fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-skw-')), log: () => {}, queryFn: fn,
      skillsDir: skillsDir(),
    });
    s.send('x');
    await tick();
    assert.deepEqual(calls.options[0].skills, ['claudecad-skills:ve-ket-cau', 'claudecad-skills:ve-kien-truc']);
    s.close();
  });

  it('reload_skills resumes the same conversation with the new skill list', async () => {
    const { fn, calls } = fakeQuery(async (input, push) => { for await (const _ of input) { push(init('conv-1')); push(result); } });
    const dir = skillsDir();
    const events: SidecarToPlugin[] = [];
    const s = new AgentSession({
      emit: (m) => events.push(m),
      bridge: { call: async (id) => ({ id, ok: true, result: {} }), status: () => {} },
      version: 'test', workDir: fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-skr-')), log: () => {}, queryFn: fn,
      skillsDir: dir,
    });
    s.send('một');
    await tick();
    fs.writeFileSync(path.join(dir, 'skills.json'), JSON.stringify({ version: 1, skills: [] })); // ve-mep turned back on
    s.reloadSkills();
    const ack = events.find((e) => e.type === 'skills_reloaded') as any;
    assert.deepEqual(ack.skills, ['ve-ket-cau', 've-kien-truc', 've-mep']);
    s.send('hai');
    await tick();
    assert.equal(calls.options.length, 2);
    assert.equal(calls.options[1].resume, 'conv-1');
    assert.ok(calls.options[1].skills.includes('claudecad-skills:ve-mep'));
    s.close();
  });

  it('parses reload_skills', () => {
    assert.deepEqual(parsePluginMessage({ type: 'reload_skills' }), { type: 'reload_skills' });
  });
});

describe('confirmation card tool', () => {
  it('emits confirm_request and tells Claude to end the turn', async () => {
    const shown: unknown[] = [];
    const [t] = interactionTools((r) => shown.push(r));
    const r = await t!.handler({ text: 'Xóa 57 đối tượng trên layer A-TEMP?', count: 57, handles: ['1A', '1B'] }, {});
    assert.match((r.content[0] as any).text, /End your turn/);
    assert.equal((shown[0] as any).type, 'confirm_request');
    assert.equal((shown[0] as any).count, 57);
    assert.deepEqual((shown[0] as any).handles, ['1A', '1B']);
  });
});
