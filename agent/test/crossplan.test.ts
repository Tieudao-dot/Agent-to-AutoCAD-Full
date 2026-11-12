/**
 * Test tổng thể (docs/ban-giao/test-tong-the.md): where the service plans and the three 3D phases meet.
 * - each plan loads only its tools, and its system prompt names no tool outside the plan;
 * - every ThreeD tool is classified for the confirmation gate, so a new 3D tool that changes existing solids
 *   cannot slip past the "always confirm" rule unnoticed;
 * - the 3D sample skill is loaded on the 3D and Full plans only.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { ALWAYS_CONFIRM, affectedCount, existingSolidsChanged } from '../src/agent/confirmation.js';
import { librarySkills, skillsForPlan } from '../src/agent/skills.js';
import { BASE_PROMPT, SYSTEM_PROMPT, TWO_D_HINTS, THREE_D_RULES, systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');
const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const all = buildTools(bridge).map((t) => t.name);

type Group = 'Common' | 'TwoD' | 'ThreeD';
/** PlanLogic's labels, read from Core/Plans.cs (the plugin's gate). */
function groups(): Map<string, Group> {
  const src = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'Plans.cs'), 'utf8');
  const out = new Map<string, Group>();
  for (const m of src.matchAll(/Add\(ToolGroup\.(\w+),([\s\S]*?)\);/g))
    for (const s of m[2]!.matchAll(/"([^"]*)"/g)) for (const n of s[1]!.split(/\s+/)) if (n) out.set(n, m[1] as Group);
  return out;
}
const G = groups();
/** PlanLogic.Allowed for a labelled tool. */
const allowed = (plan: string, t: string) => {
  const g = G.get(t);
  return g === 'Common' || (g === 'TwoD' && plan !== '3d') || (g === 'ThreeD' && plan !== '2d');
};
/** What the plugin sends as context.plan_tools (none for Full). */
const planTools = (plan: string) => [...G.keys()].filter((t) => allowed(plan, t)).sort();
const threeD = [...G.keys()].filter((t) => G.get(t) === 'ThreeD').sort();

describe('service plans × 3D', () => {
  it('2D loads no 3D tool, 3D loads no 2D-group tool, Full loads every tool', () => {
    const t2 = buildTools(bridge, planTools('2d')).map((t) => t.name);
    const t3 = buildTools(bridge, planTools('3d')).map((t) => t.name);
    assert.deepEqual(t2.filter((n) => G.get(n) === 'ThreeD'), []);
    assert.deepEqual(t3.filter((n) => G.get(n) === 'TwoD'), []);
    assert.deepEqual(threeD.filter((n) => !t3.includes(n)), [], 'every 3D tool on the 3D plan');
    assert.equal(threeD.length, 29, 'phases 1 + 2 + 3: 14 + 12 + 3 tools');
    assert.deepEqual(buildTools(bridge).map((t) => t.name).sort(), [...G.keys()].sort(), 'Full = every labelled tool');
  });

  it('the system prompt of each plan names no tool outside the plan', () => {
    for (const plan of ['2d', '3d', '2d3d'] as const) {
      const sp = systemPromptFor(plan);
      const outside = all.filter((n) => new RegExp(`\\b${n}\\b`).test(sp) && !allowed(plan, n));
      assert.deepEqual(outside, [], plan);
    }
    // the 2D drafting hints stay for the plans with the 2D tools and for an older plugin (no plan)
    assert.ok(systemPromptFor('2d').includes(TWO_D_HINTS));
    assert.ok(systemPromptFor('2d3d').includes(TWO_D_HINTS) && systemPromptFor('2d3d').includes(THREE_D_RULES));
    assert.ok(!systemPromptFor('3d').includes('# Practical geometry hints'));
    assert.ok(SYSTEM_PROMPT.includes(TWO_D_HINTS) && !SYSTEM_PROMPT.includes('# 3D modelling'));
    assert.ok(systemPromptFor('3d').startsWith(BASE_PROMPT));
  });

  it('the 3D sample skill is loaded on the 3D and Full plans only', () => {
    const dir = path.join(repo, 'skills');
    const names = librarySkills(dir);
    assert.ok(names.includes('ve-3d-kien-truc'));
    assert.ok(!skillsForPlan(dir, names, '2d').includes('ve-3d-kien-truc'));
    assert.ok(skillsForPlan(dir, names, '3d').includes('ve-3d-kien-truc'));
    assert.ok(skillsForPlan(dir, names, '2d3d').includes('ve-3d-kien-truc'));
    // the 2D samples stay on every plan
    assert.deepEqual(skillsForPlan(dir, names, '2d'), names.filter((n) => n !== 've-3d-kien-truc'));
  });
});

describe('confirmation gate × every 3D tool', () => {
  // params naming an existing solid in every way a 3D tool can
  const p = {
    handle: 'A1', handles: ['A1', 'A2'], target: 'A1', subtract: ['A2'], other_handles: ['A2'],
    profile_handles: ['B1'], section_handles: ['B1', 'B2'], erase_profile: true, keep_original: false,
  };
  /** Change an existing solid's shape: always confirmed (decided 12/11/2026). */
  const SHAPE = ['chamfer_edges', 'fillet_edges', 'shell_solid', 'slice_solid', 'solid_intersect', 'solid_subtract', 'solid_union'];
  /** Change / erase existing objects under the usual > 20 rule (profiles erased, 3D transforms). */
  const COUNTED = ['align', 'extrude', 'loft', 'mirror_3d', 'revolve', 'rotate_3d', 'sweep'];
  /** Only create, read or change the view / UCS: never change existing objects. */
  const NONE = [
    'build_3d_from_plan', 'check_interference', 'draw_box', 'draw_cylinder', 'draw_pyramid', 'draw_sphere', 'draw_torus', 'draw_wedge',
    'get_solid_info', 'projection_view', 'reset_ucs', 'section_view', 'set_ucs', 'set_view_3d', 'set_visual_style',
  ];

  it('every ThreeD tool is in exactly one class (a new 3D tool must be classified here)', () => {
    assert.deepEqual([...SHAPE, ...COUNTED, ...NONE].sort(), threeD);
  });

  it('shape changes of solids older than the turn always need the yes, also inside batch_execute', () => {
    assert.deepEqual([...ALWAYS_CONFIRM].sort(), SHAPE);
    const g = { userText: 'làm đi', answersQuestion: false };
    for (const t of SHAPE) {
      assert.ok(existingSolidsChanged(t, p, g).length > 0, t);
      assert.ok(existingSolidsChanged('batch_execute', { calls: [{ tool: 'draw_box', params: {} }, { tool: t, params: p }] }, g).length > 0, `${t} in a batch`);
      // a solid built in this turn needs none
      assert.deepEqual(existingSolidsChanged(t, p, { ...g, created: new Set(['A1', 'A2']) }), [], `${t} on new solids`);
    }
    for (const t of [...COUNTED, ...NONE]) assert.deepEqual(existingSolidsChanged(t, p, g), [], t);
  });

  it('the counted ones count, the others never do', () => {
    for (const t of COUNTED) assert.ok(affectedCount(t, p) > 0, t);
    for (const t of NONE) assert.equal(affectedCount(t, p), 0, t);
  });
});
