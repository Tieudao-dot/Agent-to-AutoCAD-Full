/**
 * 3D phase 1: the tool declarations, the plan labels (only the 3D and Full plans load them), the confirmation gate
 * for booleans on existing solids, the 3D rules of the system prompt and the 3D options of capture_view.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { z } from 'zod';
import { affectedCount, confirmationError, createdHandles, existingSolidsChanged, type GateState } from '../src/agent/confirmation.js';
import { summarizeResult } from '../src/agent/content.js';
import { SYSTEM_PROMPT, THREE_D_RULES, systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';
import { SOLID_ALIASES, SOLID_TOOL_NAMES, VIEW_PRESETS, VISUAL_STYLES } from '../src/tools/solids.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');

const calls: Array<{ tool: string; params: Record<string, unknown> }> = [];
const bridge: ToolBridge = {
  call: async (tool, params) => {
    calls.push({ tool, params });
    return { id: 'x', ok: true, result: { handles: ['1A'], volume: 2904000000, volume_m3: 2.904 } };
  },
  status: () => {},
};

const tools = buildTools(bridge);
const byName = (n: string) => tools.find((t) => t.name === n)!;
const schemaOf = (n: string) => z.object(byName(n).inputSchema as z.ZodRawShape);

/** The tools PlanLogic labels ToolGroup.ThreeD (read from Core/Plans.cs like the C# test reads the sources). */
function threeDLabels(): string[] {
  const src = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'Plans.cs'), 'utf8');
  const m = src.match(/Add\(ToolGroup\.ThreeD,([\s\S]*?)\);/);
  assert.ok(m, 'no ToolGroup.ThreeD labels in Plans.cs');
  return [...m[1]!.matchAll(/"([^"]*)"/g)].flatMap((s) => s[1]!.split(/\s+/)).filter(Boolean).sort();
}

describe('3D tools', () => {
  it('are all declared and labelled ThreeD in the plugin', () => {
    for (const n of SOLID_TOOL_NAMES) assert.ok(byName(n), n);
    assert.deepEqual(threeDLabels(), [...SOLID_TOOL_NAMES, ...SOLID_ALIASES].sort());
  });

  it('a 2D machine does not load them, a 3D machine does (with capture_view)', () => {
    // what the plugin sends as plan_tools for a 2D machine: no ThreeD tool
    const all = tools.map((t) => t.name);
    const twoD = all.filter((n) => !(SOLID_TOOL_NAMES as readonly string[]).includes(n));
    const kept2 = buildTools(bridge, twoD).map((t) => t.name);
    for (const n of SOLID_TOOL_NAMES) assert.ok(!kept2.includes(n), n);
    assert.ok(kept2.includes('capture_view'));
    const kept3 = buildTools(bridge, ['draw_polyline', 'capture_view', ...SOLID_TOOL_NAMES]).map((t) => t.name);
    for (const n of SOLID_TOOL_NAMES) assert.ok(kept3.includes(n), n);
  });

  it('schemas accept typical calls and reject bad ones', () => {
    assert.equal(schemaOf('draw_solid').safeParse({ shape: 'box', corner: [0, 0, 0], length: 4000, width: 220, height: 3300, layer: 'A-WALL-3D' }).success, true);
    assert.equal(schemaOf('draw_solid').safeParse({ shape: 'box', length: -1, width: 220, height: 3300 }).success, false);
    assert.equal(schemaOf('draw_solid').safeParse({ length: 1, width: 1, height: 1 }).success, false, 'shape is required');
    assert.equal(schemaOf('draw_solid').safeParse({ shape: 'cube', length: 1, width: 1, height: 1 }).success, false);
    assert.equal(schemaOf('draw_solid').safeParse({ shape: 'cylinder', base: [0, 0], radius: 200, height: 3300 }).success, true);
    assert.equal(schemaOf('draw_solid').safeParse({ shape: 'cylinder', base: [0, 0], radius: 200, top_radius: 0, top_center: [0, 1000, 500] }).success, true);
    assert.equal(schemaOf('draw_solid').safeParse({ shape: 'pyramid', base: [0, 0], radius: 500, height: 800, sides: 2 }).success, false);
    assert.equal(schemaOf('draw_solid').safeParse({ shape: 'torus', center: [0, 0], radius: 500, tube_radius: 50 }).success, true);
    assert.equal(schemaOf('draw_solid').safeParse({ shape: 'wedge', length: 3000, width: 1200, height: 450, rotation: 180 }).success, true);
    assert.equal(schemaOf('draw_solid').safeParse({ shape: 'sphere', center: [0, 0, 0], diameter: 600 }).success, true);
    assert.equal(schemaOf('extrude').safeParse({ profile_points: [[0, 0], [4000, 0], [4000, 6000], [0, 6000]], height: 200 }).success, true);
    assert.equal(schemaOf('extrude').safeParse({ profile_handles: ['2F4A'], height: 3300, taper_angle: 95 }).success, false);
    assert.equal(schemaOf('revolve').safeParse({ profile_points: [[0, 0], [300, 0], [300, 1000]], axis_start: [0, 0], axis_end: [0, 1] }).success, true);
    assert.equal(schemaOf('revolve').safeParse({ profile_handles: ['A'], axis_start: [0, 0], axis_end: [0, 1], angle: 0 }).success, false);
    assert.equal(schemaOf('solid_union').safeParse({ handles: ['A'] }).success, false);
    assert.equal(schemaOf('solid_subtract').safeParse({ target: 'A', subtract: ['B', 'C'] }).success, true);
    assert.equal(schemaOf('set_view_3d').safeParse({ view: 'sw_iso' }).success, true);
    assert.equal(schemaOf('set_view_3d').safeParse({ view: 'isometric' }).success, false);
    assert.equal(schemaOf('set_visual_style').safeParse({ style: 'conceptual' }).success, true);
    assert.equal(schemaOf('set_visual_style').safeParse({ style: 'toon' }).success, false);
  });

  it('view presets and styles match the plugin (Core/Solids.cs)', () => {
    const src = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'Solids.cs'), 'utf8');
    for (const v of VIEW_PRESETS) assert.ok(src.includes(`Name = "${v}"`), v);
    for (const s of VISUAL_STYLES) assert.ok(src.includes(`("${s}",`), s);
  });

  it('capture_view takes a 3D view and a visual style, and forwards them', async () => {
    const cap = z.object(byName('capture_view').inputSchema as z.ZodRawShape);
    assert.equal(cap.safeParse({ view: 'sw_iso', visual_style: 'conceptual', handles: ['2F4A'] }).success, true);
    assert.equal(cap.safeParse({ direction: [-1, -1, 1] }).success, true);
    assert.equal(cap.safeParse({ visual_style: 'cartoon' }).success, false);
    await byName('get_solid_info').handler({ handles: ['2F4A'] }, {});
    assert.deepEqual(calls.at(-1), { tool: 'get_solid_info', params: { handles: ['2F4A'] } });
  });

  it('the palette status shows the volume', () => {
    assert.match(summarizeResult('draw_box', { handles: ['1A'], volume_m3: 2.904 }).summary ?? '', /2\.904 m³/);
    assert.match(summarizeResult('get_solid_info', { solids: [], total_volume_m3: 1.5 }).summary ?? '', /1\.5 m³/);
  });
});

describe('3D confirmation gate', () => {
  const first = (extra: Partial<GateState> = {}): GateState => ({ userText: 'khoét lỗ cửa vào tường', answersQuestion: false, touched: new Set(), created: new Set(), ...extra });

  it('a boolean on a solid that existed before the turn needs a yes, even for one solid', () => {
    const err = confirmationError('solid_subtract', { target: '2f4a', subtract: ['2F4B'] }, first());
    assert.match(err ?? '', /Confirmation required: .*2 3D solid/);
    assert.match(err ?? '', /request_confirmation/);
    assert.match(confirmationError('solid_union', { handles: ['A', 'B'] }, first()) ?? '', /Confirmation required/);
    assert.match(confirmationError('solid_intersect', { handles: ['A', 'B'] }, first()) ?? '', /Confirmation required/);
  });

  it('solids built in this turn need none', () => {
    const g = first({ created: new Set(['2F4A', '2F4B']) });
    assert.equal(confirmationError('solid_subtract', { target: '2F4A', subtract: ['2F4B'] }, g), undefined);
    // keep_tools: the tool solid is not changed, so only the target counts
    assert.deepEqual(existingSolidsChanged('solid_subtract', { target: '2F4A', subtract: ['99'], keep_tools: true }, g), []);
    assert.equal(affectedCount('solid_subtract', { target: 'A', subtract: ['B', 'C'], keep_tools: true }), 1);
    assert.equal(affectedCount('solid_subtract', { target: 'A', subtract: ['B', 'C'] }), 3);
  });

  it('erasing an existing tool solid needs a yes even when the target is new (test round 1)', () => {
    // the new wall 3A is fine to change, the user's old solid 2F4B would be erased by the subtract
    const g = first({ created: new Set(['3A']) });
    assert.deepEqual(existingSolidsChanged('solid_subtract', { target: '3a', subtract: ['2f4b'] }, g), ['2F4B']);
    assert.match(confirmationError('solid_subtract', { target: '3A', subtract: ['2F4B'] }, g) ?? '', /1 3D solid/);
    // keep_tools leaves 2F4B as it is: nothing existing changes
    assert.equal(confirmationError('solid_subtract', { target: '3A', subtract: ['2F4B'], keep_tools: true }, g), undefined);
    // a union with one solid of an earlier turn
    assert.deepEqual(existingSolidsChanged('solid_union', { handles: ['3A', '1C'] }, g), ['1C']);
  });

  it('passes only after the Đồng ý button of a card listing the solids (C2, 12/11/2026)', () => {
    // expectation changed 12/11/2026 (C2, decided for the owner): a typed "đồng ý" or a button without the card's
    // handles no longer unlocks changing older solids; only the card's Đồng ý, and only for the handles it listed
    const p = { target: '2F4A', subtract: ['2F4B'] };
    assert.match(confirmationError('solid_subtract', p, first({ userText: 'đồng ý', answersQuestion: true })) ?? '', /Confirmation required/);
    assert.match(confirmationError('solid_subtract', p, first({ confirmedByButton: true })) ?? '', /Confirmation required/);
    assert.match(confirmationError('solid_subtract', p, first({ confirmedByButton: true, cardConfirmed: new Set(['2F4A']) })) ?? '', /1 3D solid.*2F4B/);
    assert.equal(confirmationError('solid_subtract', p, first({ confirmedByButton: true, cardConfirmed: new Set(['2F4A', '2F4B']) })), undefined);
    assert.match(confirmationError('solid_subtract', p, first({ userText: 'đồng ý', answersQuestion: false })) ?? '', /Only the Đồng ý button/);
  });

  it('is enforced inside batch_execute', () => {
    const batch = {
      calls: [
        { tool: 'draw_box', params: { length: 900, width: 400, height: 2100 } },
        { tool: 'solid_subtract', params: { target: '2F4A', subtract: ['2F4B'] } },
      ],
    };
    assert.match(confirmationError('batch_execute', batch, first()) ?? '', /Confirmation required/);
    assert.equal(confirmationError('batch_execute', batch, first({ created: new Set(['2F4A', '2F4B']) })), undefined);
  });

  it('extrude / revolve: erasing an existing profile counts, the new solids are "created"', () => {
    assert.equal(affectedCount('extrude', { profile_handles: ['A', 'B'], height: 100 }), 0);
    assert.equal(affectedCount('extrude', { profile_handles: ['A', 'B'], height: 100, erase_profile: true }), 2);
    assert.deepEqual(createdHandles('extrude', { profile_handles: ['A'], height: 100, erase_profile: true }, { handles: ['3C'] }), ['3C']);
    assert.deepEqual(createdHandles('draw_box', { length: 1, width: 1, height: 1 }, { handles: ['3d'] }), ['3D']);
    // a boolean's result is the changed target, not a new object
    assert.deepEqual(createdHandles('solid_union', { handles: ['A', 'B'] }, { handle: 'A', handles: ['A'] }), []);
    // erasing 25 existing profiles goes over the threshold
    const many = Array.from({ length: 25 }, (_, i) => (i + 1).toString(16));
    assert.match(confirmationError('extrude', { profile_handles: many, height: 100, erase_profile: true }, first()) ?? '', /Confirmation required/);
  });
});

describe('3D system prompt', () => {
  it('only the 3D and Full plans get the 3D rules', () => {
    assert.ok(systemPromptFor('3d').includes(THREE_D_RULES));
    assert.ok(systemPromptFor('2d3d').includes(THREE_D_RULES));
    assert.ok(!systemPromptFor('2d').includes(THREE_D_RULES));
    assert.ok(!SYSTEM_PROMPT.includes('# 3D modelling'));
  });

  it('carries the conventions of the plan', () => {
    for (const needle of ['Z is up', 'mm', 'XY plane', 'extrude', 'One layer per kind of solid', 'sw_iso', 'conceptual', 'get_solid_info', 'request_confirmation'])
      assert.ok(THREE_D_RULES.includes(needle), needle);
  });
});

describe('3D tool texts (test round 1)', () => {
  it('extrude says a positive height goes up for XY profiles, like the plugin', () => {
    const h = (byName('extrude').inputSchema as Record<string, z.ZodTypeAny>).height!;
    assert.match(h.description ?? '', /positive = up \(\+Z\)/);
  });
});
