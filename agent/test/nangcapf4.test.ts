/**
 * Nâng cấp đợt F4 (25/11/2026, docs/ban-giao/nang-cap-f4.md): the chat opens faster — a prewarmed Claude process (no
 * request, no tokens before the first message, started again when it dies) and only the most used tool schemas loaded
 * (the others deferred behind ToolSearch, none dropped) — and the schedules export to Excel.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it, mock } from 'node:test';
import { fileURLToPath } from 'node:url';
import type { SDKMessage, SDKUserMessage } from '@anthropic-ai/claude-agent-sdk';
import { AgentSession, PREWARM, TOOL_SEARCH, type QueryFn } from '../src/agent/session.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import { parsePluginMessage } from '../src/ipc/protocol.js';
import type { ToolBridge } from '../src/tools/common.js';
import { ALWAYS_LOADED_TOOLS, buildTools, createCadServer } from '../src/tools/index.js';
import { LOCAL_TOOL_NAMES, interactionTools } from '../src/tools/interaction.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');
const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const init = { type: 'system', subtype: 'init', session_id: 's1', model: 'claude-opus-5-5', apiKeySource: 'oauth', tools: [], skills: [] } as unknown as SDKMessage;
const result = { type: 'result', subtype: 'success', is_error: false, session_id: 's1' } as unknown as SDKMessage;
const tick = (ms = 20) => new Promise((r) => setTimeout(r, ms));

function groups(): Map<string, string> {
  const src = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'Plans.cs'), 'utf8');
  const out = new Map<string, string>();
  for (const m of src.matchAll(/Add\(ToolGroup\.(\w+),([\s\S]*?)\);/g))
    for (const s of m[2]!.matchAll(/"([^"]*)"/g)) for (const n of s[1]!.split(/\s+/)) if (n) out.set(n, m[1]!);
  return out;
}
const G = groups();
const planTools = (plan: string) =>
  [...G.keys()].filter((t) => G.get(t) === 'Common' || (G.get(t) === 'TwoD' && plan !== '3d') || (G.get(t) === 'ThreeD' && plan !== '2d'));

/** A fake Claude process: records every start and every message it receives; `end()` makes the process exit. */
function fakeClaude() {
  const starts: any[] = [];
  const received: string[] = [];
  const enders: Array<() => void> = [];
  const fn = ((params: { prompt: AsyncIterable<SDKUserMessage>; options: any }) => {
    starts.push(params.options);
    let stop!: () => void;
    const stopped = new Promise<void>((r) => (stop = r));
    enders.push(stop);
    const gen = (async function* () {
      const it = params.prompt[Symbol.asyncIterator]();
      while (true) {
        const next = await Promise.race([it.next(), stopped.then(() => ({ done: true, value: undefined }))]);
        if (next.done) return;
        received.push(String((next.value as SDKUserMessage).message.content));
        yield init;
        yield result;
      }
    })();
    return Object.assign(gen, { interrupt: async () => {}, close: () => stop() });
  }) as unknown as QueryFn;
  return { fn, starts, received, end: () => enders.at(-1)?.() };
}

function session(fn: QueryFn) {
  const events: any[] = [];
  const logs: string[] = [];
  const workDir = fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-f4-'));
  const s = new AgentSession({ emit: (m) => events.push(m), bridge, version: 'test', workDir, log: (l) => logs.push(l), onCancel: () => {}, queryFn: fn });
  return { s, events, logs };
}

describe('F4: prewarmed Claude process', () => {
  it('the palette can ask for it (prewarm message with plan, mode and skills)', () => {
    const m = parsePluginMessage({ type: 'prewarm', context: { service_plan: '2d', plan_tools: ['draw_line'], work_mode: '2d' }, skills: ['ve-kien-truc', 'Bad Name'] }) as any;
    assert.equal(m.type, 'prewarm');
    assert.equal(m.context.work_mode, '2d');
    assert.deepEqual(m.skills, ['ve-kien-truc']);
    assert.deepEqual(parsePluginMessage({ type: 'prewarm' }), { type: 'prewarm' });
  });

  it('starts the process without sending anything (no tokens), and the first message uses it without a restart', async () => {
    const c = fakeClaude();
    const { s } = session(c.fn);
    s.prewarm({ type: 'prewarm', context: { service_plan: '3d', plan_tools: ['draw_line', 'ping'] } });
    await tick();
    assert.equal(c.starts.length, 1);
    assert.equal(c.received.length, 0); // nothing reached Claude
    assert.equal(s.prewarmed, true);
    assert.match(c.starts[0].systemPrompt, /service plan: 3D/);
    s.sendMessage({ type: 'user_message', text: 'vẽ hộp', context: { service_plan: '3d', plan_tools: ['ping', 'draw_line'] } } as never);
    await tick();
    assert.equal(c.starts.length, 1); // the same process
    assert.equal(c.received.length, 1);
    assert.equal(s.prewarmed, false);
    s.close();
  });

  it('a first message with another plan, mode or skills restarts it as before (resumed conversation)', async () => {
    const c = fakeClaude();
    const { s } = session(c.fn);
    s.prewarm({ type: 'prewarm', context: { service_plan: '2d3d' } });
    await tick();
    s.sendMessage({ type: 'user_message', text: 'x', context: { service_plan: '2d', plan_tools: ['draw_line'], work_mode: '2d' } } as never);
    await tick();
    assert.equal(c.starts.length, 2);
    assert.match(c.starts[1].systemPrompt, /# Work mode: 2D/);
    s.close();
  });

  it('a running turn or process is left alone', async () => {
    const c = fakeClaude();
    const { s } = session(c.fn);
    s.sendMessage({ type: 'user_message', text: 'x', context: { service_plan: '2d3d' } } as never);
    await tick();
    s.prewarm({ type: 'prewarm', context: { service_plan: '2d', plan_tools: ['draw_line'] } });
    await tick();
    assert.equal(c.starts.length, 1);
    s.close();
  });

  it('a prewarmed process that dies is started again, a crash loop stops, nothing is shown in the chat', async () => {
    mock.timers.enable({ apis: ['setTimeout'] });
    try {
      const c = fakeClaude();
      const { s, events, logs } = session(c.fn);
      s.prewarm({ type: 'prewarm', context: { service_plan: '2d3d' } });
      for (let i = 0; i < PREWARM.MaxRestarts + 2; i++) {
        c.end();
        await Promise.resolve(); await Promise.resolve(); await Promise.resolve();
        for (let k = 0; k < 5; k++) await Promise.resolve();
        mock.timers.tick(PREWARM.RetryMs);
        for (let k = 0; k < 5; k++) await Promise.resolve();
      }
      assert.equal(c.starts.length, 1 + PREWARM.MaxRestarts);
      assert.equal(events.filter((e) => e.type === 'agent_error').length, 0);
      assert.ok(logs.some((l) => l.startsWith('prewarm: Claude process started')));
      s.close();
    } finally {
      mock.timers.reset();
    }
  });

  it('a new chat or another drawing gets a waiting process again', async () => {
    mock.timers.enable({ apis: ['setTimeout'] });
    try {
      const c = fakeClaude();
      const { s } = session(c.fn);
      s.prewarm({ type: 'prewarm', context: { service_plan: '2d3d' } });
      s.reset();
      mock.timers.tick(PREWARM.RetryMs);
      assert.equal(c.starts.length, 2);
      s.switchTo('s9');
      mock.timers.tick(PREWARM.RetryMs);
      assert.equal(c.starts.length, 3);
      assert.equal(c.starts[2].resume, 's9');
      s.close();
    } finally {
      mock.timers.reset();
    }
  });

  it('without a prewarm request nothing changes (older plugin)', async () => {
    const c = fakeClaude();
    const { s } = session(c.fn);
    s.reset();
    await tick();
    assert.equal(c.starts.length, 0);
    s.close();
  });
});

describe('F4: only the most used tool schemas are loaded', () => {
  for (const [plan, mode] of [['2d', undefined], ['3d', undefined], ['2d3d', undefined], ['2d', '2d'], ['3d', '3d']] as const) {
    it(`plan ${plan}${mode ? ` / mode ${mode}` : ''}: every tool of the plan is still there, none dropped`, () => {
      const allowed = plan === '2d3d' ? undefined : planTools(plan);
      const tools = buildTools(bridge, allowed, plan);
      const names = tools.map((t) => t.name).sort();
      const want = (allowed ?? buildTools(bridge).map((t) => t.name)).filter((n) => buildTools(bridge).some((t) => t.name === n)).sort();
      assert.deepEqual(names, want);
      // loaded = the always-loaded ones of this plan; the rest is deferred (name only), still callable
      const loaded = tools.filter((t) => t._meta?.['anthropic/alwaysLoad'] === true).map((t) => t.name);
      assert.deepEqual(loaded.sort(), ALWAYS_LOADED_TOOLS.filter((n) => names.includes(n)).sort());
      assert.ok(loaded.length < names.length / 3, `${loaded.length} of ${names.length} loaded`);
    });
  }

  it('every always-loaded name is a real tool and the card tools are always loaded', () => {
    const all = new Set(buildTools(bridge).map((t) => t.name));
    for (const n of ALWAYS_LOADED_TOOLS) assert.ok(all.has(n), n);
    const server = createCadServer(bridge, 't', () => {}) as any;
    assert.ok(server);
    const cards = interactionTools(() => {});
    assert.deepEqual(cards.map((t) => t.name).sort(), [...LOCAL_TOOL_NAMES].sort());
  });

  it('Claude Code gets ToolSearch and tool search on; the prompt says how to load a deferred tool', async () => {
    const c = fakeClaude();
    const { s } = session(c.fn);
    s.sendMessage({ type: 'user_message', text: 'x', context: { service_plan: '2d3d' } } as never);
    await tick();
    const o = c.starts[0];
    assert.ok(o.tools.includes('ToolSearch'));
    assert.ok(o.allowedTools.includes('ToolSearch'));
    assert.equal(o.env.ENABLE_TOOL_SEARCH, TOOL_SEARCH);
    assert.equal(TOOL_SEARCH, 'true');
    for (const p of ['2d', '3d', '2d3d'] as const) assert.match(systemPromptFor(p), /load it with ToolSearch \(query "select:mcp__claudecad__<name>"/);
    s.close();
  });
});

describe('F4: Excel export of the schedules', () => {
  for (const name of ['block_schedule', 'area_schedule', 'volume_schedule', 'check_interference']) {
    it(`${name} takes excel (true or a .xlsx name) and overwrite`, () => {
      const t = buildTools(bridge).find((x) => x.name === name)!;
      const shape = t.inputSchema as Record<string, any>;
      assert.ok(shape.excel, 'excel');
      assert.ok(shape.overwrite, 'overwrite');
      assert.equal(shape.excel.safeParse(true).success, true);
      assert.equal(shape.excel.safeParse('bang.xlsx').success, true);
      assert.equal(shape.excel.safeParse('').success, false);
    });
  }

  it('the 3D plan keeps excel on the 3D schedules (no table in the drawing, but a file)', () => {
    const tools = buildTools(bridge, planTools('3d'), '3d');
    for (const n of ['volume_schedule', 'check_interference']) {
      const shape = tools.find((t) => t.name === n)!.inputSchema as Record<string, unknown>;
      assert.ok('excel' in shape, n);
      assert.ok(!('table_position' in shape), n);
    }
  });
});
