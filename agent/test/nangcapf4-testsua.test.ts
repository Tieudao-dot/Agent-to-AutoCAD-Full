/**
 * Test & sửa đợt F4 (docs/ban-giao/nang-cap-f4-test.md): the switch-off of the deferred tools (the way back to "every
 * tool in full" without a new build), the tool set Claude Code gets (only ToolSearch more than before F4), deferred tools
 * still go through the same bridge, and the prewarm does not leak processes.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it, mock } from 'node:test';
import type { SDKMessage, SDKUserMessage } from '@anthropic-ai/claude-agent-sdk';
import { AgentSession, DEFER_OFF_FILE, DISALLOWED_BUILTINS, PREWARM, deferToolsEnabled, type QueryFn } from '../src/agent/session.js';
import { TOOL_SEARCH_HINT, systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { ALWAYS_LOADED_TOOLS, buildTools } from '../src/tools/index.js';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const tick = (ms = 20) => new Promise((r) => setTimeout(r, ms));

function fakeClaude() {
  const starts: any[] = [];
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
        yield { type: 'system', subtype: 'init', session_id: 's1', model: 'm', apiKeySource: 'oauth', tools: [], skills: [] } as unknown as SDKMessage;
        yield { type: 'result', subtype: 'success', is_error: false, session_id: 's1' } as unknown as SDKMessage;
      }
    })();
    return Object.assign(gen, { interrupt: async () => {}, close: () => stop() });
  }) as unknown as QueryFn;
  return { fn, starts, end: () => enders.at(-1)?.() };
}

function session(fn: QueryFn, deferTools?: boolean) {
  const workDir = fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-f4t-'));
  return new AgentSession({ emit: () => {}, bridge, version: 'test', workDir, log: () => {}, onCancel: () => {}, queryFn: fn, ...(deferTools === undefined ? {} : { deferTools }) });
}

describe('Test & sửa F4: switching the deferred tools off', () => {
  it('CLAUDECAD_DEFER_TOOLS=0 / false / off, or the file tool-search-off, switch it off; default on', () => {
    assert.equal(deferToolsEnabled({}, false), true);
    for (const v of ['0', 'false', 'OFF', ' no ']) assert.equal(deferToolsEnabled({ CLAUDECAD_DEFER_TOOLS: v }, false), false, v);
    assert.equal(deferToolsEnabled({ CLAUDECAD_DEFER_TOOLS: '1' }, false), true);
    assert.equal(deferToolsEnabled({}, true), false);
    assert.equal(DEFER_OFF_FILE, 'tool-search-off');
  });

  it('off: the tool set, permissions, env and prompt of before F4 (no ToolSearch at all)', () => {
    const s = session(fakeClaude().fn, false);
    const o = s.buildOptions() as any;
    assert.deepEqual(o.tools, ['Read', 'Skill']);
    assert.ok(!o.allowedTools.includes('ToolSearch'));
    assert.equal(o.env.ENABLE_TOOL_SEARCH, 'false');
    assert.ok(!String(o.systemPrompt).includes(TOOL_SEARCH_HINT));
    assert.doesNotMatch(String(o.systemPrompt), /ToolSearch/);
    s.close();
  });

  it('on (default): only ToolSearch is added; nothing that reads files, runs commands or goes on the web', () => {
    const s = session(fakeClaude().fn);
    const o = s.buildOptions() as any;
    assert.deepEqual(o.tools, ['Read', 'Skill', 'ToolSearch']);
    assert.equal(o.env.ENABLE_TOOL_SEARCH, 'true');
    assert.ok(String(o.systemPrompt).includes(TOOL_SEARCH_HINT));
    for (const t of ['Bash', 'Write', 'Edit', 'WebSearch', 'WebFetch']) assert.ok(DISALLOWED_BUILTINS.includes(t), t);
    // the only allowed rules: our MCP server, Read inside the skills folder, Skill, ToolSearch
    assert.deepEqual(o.allowedTools.filter((t: string) => !t.startsWith('mcp__claudecad') && !t.startsWith('Read(./')), ['Skill', 'ToolSearch']);
    assert.equal(o.permissionMode, 'dontAsk');
    assert.equal(o.strictMcpConfig, true);
    s.close();
  });

  it('the hint is in every plan / mode prompt when on, and taken out of each when off', () => {
    for (const [plan, mode] of [[undefined, undefined], ['2d', undefined], ['3d', undefined], ['2d3d', undefined], ['2d', '2d'], ['3d', '3d']] as const) {
      assert.ok(systemPromptFor(plan, mode).includes(TOOL_SEARCH_HINT), `${plan}/${mode}`);
      assert.ok(!systemPromptFor(plan, mode, false).includes(TOOL_SEARCH_HINT), `${plan}/${mode} off`);
    }
  });
});

describe('Test & sửa F4: deferred tools are the same tools', () => {
  it('a deferred tool calls the plugin through the same bridge (plan, read-only and the cards are checked there and in the session)', async () => {
    const calls: string[] = [];
    const rec: ToolBridge = { call: async (t) => { calls.push(t); return { id: 'x', ok: true, result: {} }; }, status: () => {} };
    const tools = buildTools(rec);
    const deferred = tools.filter((t) => !(t as any)._meta?.['anthropic/alwaysLoad']);
    assert.ok(deferred.length > 50);
    for (const name of ['rotate_entities', 'solid_union', 'set_material', 'block_schedule']) {
      const t = deferred.find((x) => x.name === name)!;
      assert.ok(t, name);
      await (t as any).handler({}, {});
    }
    assert.deepEqual(calls, ['rotate_entities', 'solid_union', 'set_material', 'block_schedule']);
  });

  it('every always-loaded name is a real tool, listed once', () => {
    const always = new Set(ALWAYS_LOADED_TOOLS);
    const all = buildTools(bridge).map((t) => t.name);
    for (const n of ALWAYS_LOADED_TOOLS) assert.ok(all.includes(n), n);
    assert.equal(always.size, ALWAYS_LOADED_TOOLS.length, 'no duplicates');
  });
});

describe('Test & sửa F4: the prewarm does not leak processes', () => {
  it('many prewarm requests: one process', async () => {
    const c = fakeClaude();
    const s = session(c.fn);
    for (let i = 0; i < 5; i++) s.prewarm({ type: 'prewarm', context: { service_plan: '2d3d' } });
    await tick();
    assert.equal(c.starts.length, 1);
    s.close();
  });

  it('after close (AutoCAD closing) a pending restart never starts a process', async () => {
    mock.timers.enable({ apis: ['setTimeout'] });
    try {
      const c = fakeClaude();
      const s = session(c.fn);
      s.prewarm({ type: 'prewarm', context: { service_plan: '2d3d' } });
      s.reset(); // schedules a new waiting process
      s.close();
      mock.timers.tick(PREWARM.RetryMs * 3);
      for (let k = 0; k < 5; k++) await Promise.resolve();
      assert.equal(c.starts.length, 1);
    } finally {
      mock.timers.reset();
    }
  });
});
