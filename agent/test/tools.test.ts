/**
 * Consistency between the tools declared to Claude (sidecar) and the handlers
 * registered in the C# plugin, plus schema sanity checks.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { z } from 'zod';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';
import { ERROR_CODES, parsePluginMessage } from '../src/ipc/protocol.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');
const pluginDir = path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Drawing');

function pluginToolNames(): Set<string> {
  const names = new Set<string>();
  for (const f of fs.readdirSync(pluginDir)) {
    if (!f.endsWith('.cs')) continue;
    const src = fs.readFileSync(path.join(pluginDir, f), 'utf8');
    for (const m of src.matchAll(/(?:\.Register\(|new ToolDef\()\s*"([a-z0-9_]+)"/g)) names.add(m[1]!);
  }
  return names;
}

const calls: Array<{ tool: string; params: Record<string, unknown> }> = [];
const bridge: ToolBridge = {
  call: async (tool, params) => {
    calls.push({ tool, params });
    return { id: 'x', ok: true, result: { handles: ['1A'] } };
  },
  status: () => {},
};

describe('tool catalogue', () => {
  const tools = buildTools(bridge);

  it('has unique names', () => {
    const names = tools.map((t) => t.name);
    assert.equal(new Set(names).size, names.length);
  });

  it('every sidecar tool has a C# handler and vice versa', () => {
    const ts = new Set(tools.map((t) => t.name));
    const cs = pluginToolNames();
    const missingInPlugin = [...ts].filter((n) => !cs.has(n));
    const missingInSidecar = [...cs].filter((n) => !ts.has(n));
    assert.deepEqual(missingInPlugin, [], 'declared to Claude but not handled by the plugin');
    assert.deepEqual(missingInSidecar, [], 'handled by the plugin but not declared to Claude');
  });

  it('every input schema converts to JSON Schema (required by the MCP server)', () => {
    for (const t of tools) {
      const js = z.toJSONSchema(z.object(t.inputSchema as z.ZodRawShape)) as { type?: string };
      assert.equal(js.type, 'object', t.name);
    }
  });

  it('descriptions are substantial English text', () => {
    for (const t of tools) assert.ok(t.description.length >= 40, `${t.name} description too short`);
  });

  it('schemas accept the spec example and reject bad points', () => {
    const pl = tools.find((t) => t.name === 'draw_polyline')!;
    const schema = z.object(pl.inputSchema as z.ZodRawShape);
    assert.equal(schema.safeParse({ vertices: [[0, 0], [4000, 0], [4000, 6000], [0, 6000]], closed: true, layer: 'A-WALL' }).success, true);
    assert.equal(schema.safeParse({ vertices: [[0]] }).success, false);
    const batch = tools.find((t) => t.name === 'batch_execute')!;
    const bs = z.object(batch.inputSchema as z.ZodRawShape);
    assert.equal(bs.safeParse({ calls: [{ tool: 'draw_line', params: { start: [0, 0], end: [1, 1] } }] }).success, true);
    assert.equal(bs.safeParse({ calls: Array.from({ length: 501 }, () => ({ tool: 'draw_line' })) }).success, false);
  });

  it('tool bodies forward params unchanged (minus undefined) to the plugin', async () => {
    const line = tools.find((t) => t.name === 'draw_line')!;
    const r = await line.handler({ start: [0, 0], end: [4000, 0], layer: undefined }, {});
    assert.deepEqual(calls.at(-1), { tool: 'draw_line', params: { start: [0, 0], end: [4000, 0] } });
    assert.deepEqual(JSON.parse((r.content[0] as any).text), { handles: ['1A'] });
  });
});

/** Tool names labelled in the plugin's Core/Plans.cs (PlanLogic), the gate of the service plans. */
function planLabels(): Set<string> {
  const src = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'Plans.cs'), 'utf8');
  const names = new Set<string>();
  for (const m of src.matchAll(/Add\(ToolGroup\.\w+,([\s\S]*?)\);/g))
    for (const s of m[1]!.matchAll(/"([^"]*)"/g)) for (const n of s[1]!.split(/\s+/)) if (n) names.add(n);
  return names;
}

describe('service plan tools', () => {
  it('every tool declared to Claude has a plan label in the plugin', () => {
    const labels = planLabels();
    assert.ok(labels.size >= 90);
    const missing = buildTools(bridge).map((t) => t.name).filter((n) => !labels.has(n));
    assert.deepEqual(missing, []);
  });

  it('keeps only the tools of the plan, capture_view and ping included', () => {
    const kept = buildTools(bridge, ['draw_line', 'capture_view', 'ping', 'not_a_tool']).map((t) => t.name).sort();
    assert.deepEqual(kept, ['capture_view', 'draw_line', 'ping']);
    assert.equal(buildTools(bridge, undefined).length, buildTools(bridge).length);
  });

  it('NOT_IN_PLAN is a known error code', () => {
    assert.ok((ERROR_CODES as readonly string[]).includes('NOT_IN_PLAN'));
  });

  it('reads the plan and its tools from the message context, drops unknown plans and bad names', () => {
    const m = parsePluginMessage({ type: 'user_message', text: 'x', context: { service_plan: '3d', plan_tools: ['draw_line', 'Bad Name', 7] } }) as any;
    assert.equal(m.context.service_plan, '3d');
    assert.deepEqual(m.context.plan_tools, ['draw_line']);
    const u = parsePluginMessage({ type: 'user_message', text: 'x', context: { service_plan: '4d', plan_tools: ['draw_line'] } }) as any;
    assert.equal(u.context, undefined);
  });
});
