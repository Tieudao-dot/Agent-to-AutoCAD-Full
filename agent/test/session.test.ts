/**
 * AgentSession with a fake query(): checks the SDK-event -> palette-event mapping,
 * streaming input, cancel and new session, without calling Claude.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it } from 'node:test';
import type { SDKMessage, SDKUserMessage } from '@anthropic-ai/claude-agent-sdk';
import { AgentSession, type QueryFn } from '../src/agent/session.js';
import type { SidecarToPlugin } from '../src/ipc/protocol.js';

function fakeQuery(script: (input: AsyncIterable<SDKUserMessage>, push: (m: SDKMessage) => void) => Promise<void>) {
  const calls: { options: any; interrupted: number; closed: number } = { options: undefined, interrupted: 0, closed: 0 };
  const fn = ((params: { prompt: AsyncIterable<SDKUserMessage>; options: any }) => {
    calls.options = params.options;
    const queue: SDKMessage[] = [];
    let wake: (() => void) | undefined;
    let done = false;
    let failure: unknown;
    const push = (m: SDKMessage) => {
      queue.push(m);
      wake?.();
    };
    script(params.prompt, push)
      .catch((err) => {
        failure = err;
      })
      .finally(() => {
        done = true;
        wake?.();
      });
    const gen = (async function* () {
      while (true) {
        if (queue.length) {
          yield queue.shift()!;
          continue;
        }
        if (done) {
          if (failure) throw failure;
          return;
        }
        await new Promise<void>((r) => (wake = r));
        wake = undefined;
      }
    })();
    return Object.assign(gen, {
      interrupt: async () => {
        calls.interrupted++;
      },
      close: () => {
        calls.closed++;
      },
    });
  }) as unknown as QueryFn;
  return { fn, calls };
}

const textDelta = (text: string): SDKMessage =>
  ({
    type: 'stream_event',
    parent_tool_use_id: null,
    uuid: 'u',
    session_id: 's1',
    event: { type: 'content_block_delta', index: 0, delta: { type: 'text_delta', text } },
  }) as unknown as SDKMessage;
const blockStop = { type: 'stream_event', parent_tool_use_id: null, uuid: 'u', session_id: 's1', event: { type: 'content_block_stop', index: 0 } } as unknown as SDKMessage;
const init = { type: 'system', subtype: 'init', session_id: 's1', model: 'claude-opus-5-5', apiKeySource: 'oauth', tools: [], skills: ['ve-kien-truc'] } as unknown as SDKMessage;
const result = (subtype = 'success') => ({ type: 'result', subtype, is_error: subtype !== 'success', session_id: 's1' }) as unknown as SDKMessage;

function makeSession(fn: QueryFn, skillsDir?: string) {
  const events: SidecarToPlugin[] = [];
  let cancelled = 0;
  const workDir = fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-test-'));
  const s = new AgentSession({
    emit: (m) => events.push(m),
    bridge: { call: async (id) => ({ id, ok: true, result: {} }), status: () => {} },
    version: 'test',
    workDir,
    skillsDir,
    log: () => {},
    onCancel: () => cancelled++,
    queryFn: fn,
  });
  return { s, events, workDir, cancelled: () => cancelled };
}

const tick = (ms = 20) => new Promise((r) => setTimeout(r, ms));

describe('AgentSession', () => {
  it('echoes the turn id of the message in turn_end', async () => {
    const { fn } = fakeQuery(async (input, push) => {
      for await (const _m of input) {
        push(init);
        push(result());
      }
    });
    const { s, events } = makeSession(fn);
    s.sendMessage({ type: 'user_message', text: 'x', turn_id: 't-7' });
    await tick();
    const end = events.find((e) => e.type === 'turn_end') as { turn_id?: string };
    assert.equal(end.turn_id, 't-7');
  });

  it('streams text deltas and ends the turn on result', async () => {
    const received: string[] = [];
    const { fn, calls } = fakeQuery(async (input, push) => {
      for await (const m of input) {
        received.push(String(m.message.content));
        push(init);
        push(textDelta('Xin '));
        push(textDelta('chào!'));
        push(blockStop);
        push(result());
      }
    });
    const { s, events } = makeSession(fn);
    s.send('chào Claude', 'test.dwg');
    await tick();
    assert.deepEqual(received, ['[Bản vẽ đang mở: "test.dwg"]\nchào Claude']);
    assert.deepEqual(events.filter((e) => e.type !== 'session_info').map((e) => (e.type === 'turn_end' ? { type: e.type } : e)), [
      { type: 'ready', info: 'claude-opus-5-5 · Claude login', version: 'test' },
      { type: 'agent_text', delta: 'Xin ' },
      { type: 'agent_text', delta: 'chào!' },
      { type: 'agent_text_end' },
      { type: 'turn_end' },
    ]);
    assert.equal(s.busy, false);
    // permission / isolation configuration (spec §5)
    assert.equal(calls.options.permissionMode, 'dontAsk');
    assert.deepEqual(calls.options.tools, ['Read', 'Skill']);
    assert.ok(calls.options.disallowedTools.includes('Bash'));
    assert.ok(calls.options.disallowedTools.includes('WebSearch'));
    assert.ok(calls.options.allowedTools.includes('mcp__claudecad'));
    assert.equal(calls.options.includePartialMessages, true);
    assert.ok(calls.options.mcpServers.claudecad);
    s.close();
  });

  it('keeps one conversation across turns (streaming input)', async () => {
    let starts = 0;
    const turns: string[] = [];
    const { fn } = fakeQuery(async (input, push) => {
      starts++;
      for await (const m of input) {
        turns.push(String(m.message.content));
        push(result());
      }
    });
    const { s, events } = makeSession(fn);
    s.send('một');
    await tick();
    s.send('hai');
    await tick();
    assert.equal(starts, 1);
    assert.deepEqual(turns, ['một', 'hai']);
    assert.equal(events.filter((e) => e.type === 'turn_end').length, 2);
    s.close();
  });

  it('cancel interrupts the query and reports a cancelled turn', async () => {
    const { fn, calls } = fakeQuery(async (input, push) => {
      for await (const _ of input) {
        push(textDelta('đang vẽ...'));
        await tick(50);
        push(result('error_during_execution'));
      }
    });
    const { s, events, cancelled } = makeSession(fn);
    s.send('vẽ 1000 đường');
    await tick(10);
    await s.cancel();
    await tick(80);
    assert.equal(calls.interrupted, 1);
    assert.equal(cancelled(), 1);
    const end = events.find((e) => e.type === 'turn_end') as any;
    assert.equal(end.cancelled, true);
    assert.equal(end.error, undefined);
    s.close();
  });

  it('new_session closes the old query and starts a fresh one', async () => {
    let starts = 0;
    const { fn, calls } = fakeQuery(async (input, push) => {
      starts++;
      for await (const _ of input) push(result());
    });
    const { s, events } = makeSession(fn);
    s.send('a');
    await tick();
    s.reset();
    assert.equal(calls.closed, 1);
    assert.ok(events.some((e) => e.type === 'session_reset'));
    s.send('b');
    await tick();
    assert.equal(starts, 2);
    assert.equal(calls.options.resume, undefined);
    s.close();
  });

  it('ends a pending turn with an error if the Claude process dies', async () => {
    const { fn } = fakeQuery(async (input) => {
      for await (const _ of input) throw new Error('process exited with code 1');
    });
    const { s, events } = makeSession(fn);
    s.send('x');
    await tick(50);
    assert.ok(events.some((e) => e.type === 'agent_error'));
    const end = events.find((e) => e.type === 'turn_end') as any;
    assert.match(end.error, /Claude/);
    assert.equal(s.busy, false);
    s.close();
  });

  it('maps an authentication error to a Vietnamese hint', async () => {
    const { fn } = fakeQuery(async (input, push) => {
      for await (const _ of input) {
        push({ type: 'assistant', error: 'authentication_failed', parent_tool_use_id: null, message: { content: [] } } as unknown as SDKMessage);
        push(result());
      }
    });
    const { s, events } = makeSession(fn);
    s.send('x');
    await tick();
    const err = events.find((e) => e.type === 'agent_error') as any;
    assert.match(err.message, /Đăng nhập Claude/);
    assert.equal(err.code, 'authentication_failed'); // the palette shows its login button for this code
    s.close();
  });

  it('builds a local skills plugin from the skills folder', async () => {
    const skillsDir = fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-skills-'));
    fs.mkdirSync(path.join(skillsDir, 've-test'));
    fs.writeFileSync(path.join(skillsDir, 've-test', 'SKILL.md'), '---\nname: ve-test\ndescription: test\n---\nbody');
    fs.mkdirSync(path.join(skillsDir, 'not-a-skill'));
    const { fn, calls } = fakeQuery(async (input, push) => {
      for await (const _ of input) push(result());
    });
    const { s, workDir } = makeSession(fn, skillsDir);
    s.send('x');
    await tick();
    const plugin = calls.options.plugins[0];
    assert.equal(plugin.type, 'local');
    assert.ok(fs.existsSync(path.join(plugin.path, '.claude-plugin', 'plugin.json')));
    assert.ok(fs.existsSync(path.join(plugin.path, 'skills', 've-test', 'SKILL.md')));
    assert.equal(fs.existsSync(path.join(plugin.path, 'skills', 'not-a-skill')), false);
    assert.deepEqual(calls.options.skills, ['claudecad-skills:ve-test']);
    assert.ok(plugin.path.startsWith(workDir));
    s.close();
  });
});

describe('system prompt (spec §9)', async () => {
  const { SYSTEM_PROMPT } = await import('../src/agent/systemPrompt.js');
  it('covers the ten behaviour rules', () => {
    for (const needle of [
      'Vietnamese', 'millimetres', 'get_drawing_info', 'list_layers', 'batch_execute', 'Never guess handles',
      'Cậu có đồng ý không?', 'save_drawing', 'purge_unused', 'U once', 'at most 2 times', 'ask one short question', 'skill',
    ]) assert.ok(SYSTEM_PROMPT.includes(needle), needle);
  });
});

describe('v0.9.4 review fixes', () => {
  it('a late result of a cancelled turn does not end the next turn', async () => {
    let release: (() => void) | undefined;
    const { fn } = fakeQuery(async (input, push) => {
      let n = 0;
      for await (const _m of input) {
        n++;
        push(init);
        if (n === 1) {
          // first turn: interrupted, its result only comes much later (after the next message was sent)
          await new Promise<void>((r) => (release = r));
          push(result());
          push(init); // the second turn starts after the old result
          push(textDelta('lượt 2'));
          await tick(50);
          push(result());
        }
      }
    });
    const { s, events } = makeSession(fn);
    s.sendMessage({ type: 'user_message', text: 'vẽ', turn_id: 't-1' });
    await tick();
    await s.cancel();
    await tick(3200); // cancel fallback ends turn 1
    assert.equal(events.filter((e) => e.type === 'turn_end').length, 1);
    s.sendMessage({ type: 'user_message', text: 'vẽ lại', turn_id: 't-2' });
    release!();
    await tick(20);
    // the old result was skipped: turn 2 is still running
    assert.equal(s.busy, true);
    await tick(100);
    const ends = events.filter((e) => e.type === 'turn_end') as Array<{ turn_id?: string }>;
    assert.deepEqual(ends.map((e) => e.turn_id), ['t-1', 't-2']);
  });

  it('an abandoned turn that never reports a result does not swallow the next result', async () => {
    const { fn } = fakeQuery(async (input, push) => {
      let n = 0;
      for await (const _m of input) {
        n++;
        if (n === 1) continue; // turn 1: never answers
        push(init);
        push(result());
      }
    });
    const { s, events } = makeSession(fn);
    s.sendMessage({ type: 'user_message', text: 'a', turn_id: 't-1' });
    await tick();
    s.sendMessage({ type: 'user_message', text: 'b', turn_id: 't-2' }); // palette force-ended turn 1
    await tick();
    const ends = events.filter((e) => e.type === 'turn_end') as Array<{ turn_id?: string }>;
    assert.deepEqual(ends.map((e) => e.turn_id), ['t-2']);
  });

  it('"success" with is_error reports the API error text', async () => {
    const { fn } = fakeQuery(async (input, push) => {
      for await (const _m of input) {
        push(init);
        push({ type: 'result', subtype: 'success', is_error: true, result: 'API Error: 500 internal', session_id: 's1' } as unknown as SDKMessage);
      }
    });
    const { s, events } = makeSession(fn);
    s.sendMessage({ type: 'user_message', text: 'x' });
    await tick();
    const end = events.find((e) => e.type === 'turn_end') as { error?: string };
    assert.match(end.error ?? '', /API Error: 500/);
  });

  it('confirmation gate: cumulative per turn, a short yes answers a question of the previous turn', async () => {
    const { fn } = fakeQuery(async (input, push) => {
      let n = 0;
      for await (const _m of input) {
        n++;
        push(init);
        if (n === 1) push(textDelta('Có 40 đối tượng trên layer A-TEMP. Cậu có đồng ý không?'));
        push(result());
      }
    });
    const { s } = makeSession(fn);
    const h = (k: number) => ({ handles: Array.from({ length: k }, (_, i) => `H${i}`) });
    s.sendMessage({ type: 'user_message', text: 'dọn layer A-TEMP' });
    assert.equal(s.gate('erase_entities', h(20)), undefined);
    assert.match(s.gate('erase_entities', h(20)) ?? '', /Confirmation required/); // 40 in total
    await tick();
    s.sendMessage({ type: 'user_message', text: 'đồng ý' });
    assert.equal(s.gate('erase_entities', h(40)), undefined);
    await tick();
    // a new request with "ok" inside, after a turn that asked nothing → asked again
    s.sendMessage({ type: 'user_message', text: 'ok' });
    await tick();
    s.sendMessage({ type: 'user_message', text: 'ok xóa hết' });
    assert.match(s.gate('erase_entities', h(30)) ?? '', /Confirmation required/);
  });

  it('no SDK next-step prediction when Claude made its own buttons or the user turned them off', async () => {
    const suggestion = { type: 'prompt_suggestion', suggestion: 'vẽ cửa', session_id: 's1' } as unknown as SDKMessage;
    const { fn } = fakeQuery(async (input, push) => {
      for await (const _m of input) {
        push(init);
        push(result());
        push(suggestion);
      }
    });
    const { s, events } = makeSession(fn);
    s.sendMessage({ type: 'user_message', text: 'a', context: { suggestions: false } });
    await tick();
    assert.equal(events.filter((e) => e.type === 'suggestions').length, 0);
    s.sendMessage({ type: 'user_message', text: 'b' });
    await tick();
    assert.equal(events.filter((e) => e.type === 'suggestions').length, 1);
  });

  it('switching drawing mid-turn ends the turn', async () => {
    const { fn } = fakeQuery(async (input) => {
      for await (const _m of input) {
        /* never answers */
      }
    });
    const { s, events } = makeSession(fn);
    s.sendMessage({ type: 'user_message', text: 'a', turn_id: 't-1' });
    await tick();
    s.switchTo('other');
    const end = events.find((e) => e.type === 'turn_end') as { turn_id?: string; cancelled?: boolean };
    assert.equal(end.turn_id, 't-1');
    assert.equal(end.cancelled, true);
    assert.equal(s.busy, false);
  });

  it('Read is only allowed inside the skills plugin', () => {
    const { fn, calls } = fakeQuery(async () => {});
    const { s } = makeSession(fn);
    s.sendMessage({ type: 'user_message', text: 'x' });
    const allowed = calls.options.allowedTools as string[];
    assert.ok(!allowed.includes('Read'));
    assert.ok(allowed.includes('Read(./claudecad-skills/**)'));
  });
});

describe('skills per chat', () => {
  function library(names: string[]): string {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-skills-'));
    for (const n of names) {
      fs.mkdirSync(path.join(dir, n));
      fs.writeFileSync(path.join(dir, n, 'SKILL.md'), `---\nname: ${n}\ndescription: Skill ${n} dùng khi vẽ ${n}.\n---\n# ${n}\n`);
    }
    // skills.json says ve-mep is off: per-chat lists ignore it
    fs.writeFileSync(path.join(dir, 'skills.json'), JSON.stringify({ skills: [{ name: 've-mep', enabled: false }] }));
    return dir;
  }

  it('loads exactly the chat\'s skills, restarts Claude (same conversation) when the list changes, tells Claude about − / +', async () => {
    const texts: string[] = [];
    const starts: any[] = [];
    const fn = ((params: { prompt: AsyncIterable<SDKUserMessage>; options: any }) => {
      starts.push(params.options);
      const gen = (async function* () {
        for await (const m of params.prompt) {
          texts.push(String(m.message.content));
          yield init;
          yield result();
        }
      })();
      return Object.assign(gen, { interrupt: async () => {}, close: () => {} });
    }) as unknown as QueryFn;
    const dir = library(['khung-ten', 've-kien-truc', 've-mep']);
    const { s } = makeSession(fn, dir);
    const sent = (skills: string[]) => s.sendMessage({ type: 'user_message', text: 'vẽ', skills });

    sent(['ve-kien-truc', 'khung-ten']);
    await tick();
    assert.equal(starts.length, 1);
    assert.deepEqual(starts[0].skills, ['claudecad-skills:khung-ten', 'claudecad-skills:ve-kien-truc']);

    sent(['khung-ten', 've-kien-truc']); // same skills, other order: no restart
    await tick();
    assert.equal(starts.length, 1);

    sent(['khung-ten', 've-kien-truc', 've-mep']); // "+" ve-mep (off in skills.json: the chat's list wins)
    await tick();
    assert.equal(starts.length, 2);
    assert.ok(starts[1].skills.includes('claudecad-skills:ve-mep'));
    assert.equal(starts[1].resume, 's1'); // same conversation
    assert.match(texts.at(-1)!, /vừa thêm skill vào đoạn chat này: "ve-mep"/);

    sent(['ve-mep']); // "−" khung-ten and ve-kien-truc
    await tick();
    assert.equal(starts.length, 3);
    assert.deepEqual(starts[2].skills, ['claudecad-skills:ve-mep']);
    assert.match(texts.at(-1)!, /đã bỏ skill khỏi đoạn chat này: "khung-ten", "ve-kien-truc" — from now on do not apply/);

    s.switchTo('other-drawing'); // another chat: no − / + note for its first message
    sent(['khung-ten']);
    await tick();
    assert.doesNotMatch(texts.at(-1)!, /đã bỏ skill|vừa thêm skill/);
  });

  it('an older plugin without a list still gets the enabled skills', async () => {
    const { fn, calls } = fakeQuery(async () => {});
    const dir = library(['ve-kien-truc', 've-mep']);
    const { s } = makeSession(fn, dir);
    s.sendMessage({ type: 'user_message', text: 'x' });
    assert.deepEqual(calls.options.skills, ['claudecad-skills:ve-kien-truc']);
  });
});
