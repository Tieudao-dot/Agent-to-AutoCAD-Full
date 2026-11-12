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
import { ALWAYS_CONFIRM, PLUGIN_CONFIRMED, USER_CONFIRMED_PARAM, affectedCount, applyUserConfirmation, confirmationError, existingSolidsChanged, refusedForConfirmation, type GateState } from '../src/agent/confirmation.js';
import { SAMPLE_2D_SKILLS, librarySkills, skillPlan, skillsForPlan } from '../src/agent/skills.js';
import os from 'node:os';
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

  it('the 3D sample skill is loaded on the 3D and Full plans only, the 2D samples on the 2D and Full plans only', () => {
    const dir = path.join(repo, 'skills');
    const names = librarySkills(dir);
    assert.ok(names.includes('ve-3d-kien-truc'));
    assert.ok(!skillsForPlan(dir, names, '2d').includes('ve-3d-kien-truc'));
    assert.ok(skillsForPlan(dir, names, '3d').includes('ve-3d-kien-truc'));
    assert.ok(skillsForPlan(dir, names, '2d3d').includes('ve-3d-kien-truc'));
    // the 2D samples ("plan: 2d", decided 12/11/2026): 2D and Full only
    const twoD = ['ve-ha-tang', 've-kien-truc', 've-mep'];
    assert.deepEqual(skillsForPlan(dir, names, '2d'), names.filter((n) => n !== 've-3d-kien-truc'));
    assert.deepEqual(skillsForPlan(dir, names, '3d').filter((n) => twoD.includes(n)), []);
    assert.deepEqual(skillsForPlan(dir, names, '2d3d'), names);
    for (const n of twoD) assert.ok(names.includes(n), n);
    // the 3D machine still knows the plan conventions build_3d_from_plan reads (layers, wall lines, door / window symbols)
    const md = fs.readFileSync(path.join(dir, 've-3d-kien-truc', 'SKILL.md'), 'utf8');
    for (const needle of ['A-WALL', 'A-DOOR', 'A-WIND', 'A-SLAB', 'A-FLOR', 'CUA-DI-', 'CUA-SO-', '220', '110'])
      assert.ok(md.includes(needle), needle);
    // without the 2D-only parts of ve-kien-truc (text heights, dimension styles)
    for (const needle of ['add_dim_linear', 'KT-ARIAL', 'create_dim_style', 'add_text']) assert.ok(!md.includes(needle), needle);
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

describe('explode_entities × 3D solids (decided 12/11/2026)', () => {
  const yes = { userText: 'đồng ý', answersQuestion: true };
  const no = { userText: 'phá khối đó ra', answersQuestion: false };

  it('the plugin reads the handles approved on the card; a typed yes, the plan button or Claude never set them (C1 / C2)', () => {
    // expectation changed 12/11/2026 (C1 / C2, decided for the owner): erase_entities is checked too, and user_confirmed
    // carries the call's handles that were on the card whose Đồng ý started the turn (it was a plain true after any yes)
    assert.deepEqual([...PLUGIN_CONFIRMED], ['explode_entities', 'erase_entities']);
    const plans = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'Solids.cs'), 'utf8');
    assert.ok(plans.includes(`UserConfirmedParam = "${USER_CONFIRMED_PARAM}"`), 'same name as SolidLogic.UserConfirmedParam');
    assert.ok(plans.includes('CardConfirmTools = new[] { "explode_entities", "erase_entities" }'), 'same tools as SolidLogic.CardConfirmTools');
    const card = { ...no, confirmedByButton: true, cardConfirmed: new Set(['A', 'C']) };
    assert.deepEqual(applyUserConfirmation('explode_entities', { handles: ['a', 'B'] }, card), { handles: ['a', 'B'], user_confirmed: ['A'] });
    assert.deepEqual(applyUserConfirmation('erase_entities', { handles: ['C'] }, card), { handles: ['C'], user_confirmed: ['C'] });
    assert.deepEqual(applyUserConfirmation('explode_entities', { handles: ['B'] }, card), { handles: ['B'] }); // not on the card
    // a typed yes or the plan card's "Làm" approve nothing here
    assert.deepEqual(applyUserConfirmation('explode_entities', { handles: ['A'] }, yes), { handles: ['A'] });
    assert.deepEqual(applyUserConfirmation('explode_entities', { handles: ['A'] }, { ...no, confirmedByButton: true }), { handles: ['A'] });
    // whatever Claude puts there is replaced
    assert.deepEqual(applyUserConfirmation('explode_entities', { handles: ['A'], user_confirmed: ['A'] }, no), { handles: ['A'] });
    const batch = { calls: [{ tool: 'erase_entities', params: { handles: ['A'], user_confirmed: true } }, { tool: 'move_entities', params: { handles: ['B'], user_confirmed: true } }] };
    assert.deepEqual(applyUserConfirmation('batch_execute', batch, no).calls, [
      { tool: 'erase_entities', params: { handles: ['A'] } },
      { tool: 'move_entities', params: { handles: ['B'], user_confirmed: true } }, // the plugin ignores it on other tools
    ]);
    assert.deepEqual(applyUserConfirmation('batch_execute', { calls: [{ tool: 'explode_entities', params: { handles: ['C'] } }] }, card).calls,
      [{ tool: 'explode_entities', params: { handles: ['C'], user_confirmed: ['C'] } }]);
    const plain = { handles: ['A'] };
    assert.equal(applyUserConfirmation('set_entity_props', plain, card), plain);
    const b = { calls: [{ tool: 'erase_entities', params: plain }] };
    assert.equal(applyUserConfirmation('batch_execute', b, yes), b); // nothing to flag: sent as is
  });

  it('KT-09 (Kiểm tra độc lập; test copied from full/kiem-tra-doc-lap): a plain "ok" in chat is not the confirmation card', () => {
    // Claude asked something unrelated in the previous turn ("Tường cao 3 m nhé?" sets answersQuestion via the "?"),
    // the user answered "ok": changing solids that existed before the turn still needs the card (C2, 12/11/2026)
    const g: GateState = { userText: 'ok', answersQuestion: true, touched: new Set(), created: new Set() };
    const err = confirmationError('solid_union', { handles: ['2F4A', '2F4B'] }, g);
    assert.ok(err, 'solid_union on two older solids ran without the confirmation card after a text "ok"');
    // the plan card's "Làm" is not the card either
    assert.ok(confirmationError('slice_solid', { handle: '2F4A' }, { ...g, confirmedByButton: true }));
    // the card's Đồng ý with these handles is
    assert.equal(confirmationError('solid_union', { handles: ['2f4a', '2F4B'] }, { ...g, confirmedByButton: true, cardConfirmed: new Set(['2F4A', '2F4B']) }), undefined);
    // the 2D rule (> 20 objects) is unchanged: a typed yes still answers it
    const many = { handles: Array.from({ length: 25 }, (_, i) => `H${i}`) };
    assert.ok(confirmationError('erase_entities', many, { ...g, answersQuestion: false }));
    assert.equal(confirmationError('erase_entities', many, g), undefined);
  });

  it('KT-37: 2D tools that change or remove existing objects count for the > 20 rule (a small change of the 2D rule)', () => {
    assert.equal(affectedCount('fillet_entities', { handle1: 'A', handle2: 'B', radius: 5 }), 2);
    assert.equal(affectedCount('chamfer_entities', { handle1: 'A', handle2: 'B', dist1: 5 }), 2);
    assert.equal(affectedCount('define_block', { name: 'X', entity_handles: ['A', 'B', 'C'] }), 3);
    assert.equal(affectedCount('define_block', { name: 'X', entity_handles: ['A'], replace_with_insert: false }), 0);
    assert.equal(affectedCount('delete_layout', { name: 'A1' }), 1);
    // 11 fillets of 2 lines each = 22 existing objects in one batch: asks
    const calls = Array.from({ length: 11 }, (_, i) => ({ tool: 'fillet_entities', params: { handle1: `L${2 * i}`, handle2: `L${2 * i + 1}`, radius: 0 } }));
    assert.match(confirmationError('batch_execute', { calls }, { userText: 'bo góc', answersQuestion: false }) ?? '', /Confirmation required/);
  });

  it('the plugin refusal is recognised alone and inside a batch', () => {
    assert.ok(refusedForConfirmation({ ok: false, error: { message: 'Confirmation required: explode_entities would break up 1 3D solid(s)' } }));
    assert.ok(!refusedForConfirmation({ ok: false, error: { message: 'explode_entities: entity not found' } }));
    assert.ok(refusedForConfirmation({ ok: true, result: { results: [{ ok: true }, { ok: false, error: { message: 'Confirmation required: …' } }] } }));
    assert.ok(!refusedForConfirmation({ ok: true, result: { results: [{ ok: false, error: { message: 'LOCKED' } }] } }));
    assert.ok(!refusedForConfirmation({ ok: true, result: { handles: ['A'] } }));
  });

  it('explode_entities tells Claude to ask first; the 2D rule (> 20 objects) is unchanged', () => {
    const t = buildTools(bridge).find((x) => x.name === 'explode_entities')!;
    assert.match(t.description, /3D solids, surfaces and meshes that existed before this turn/);
    assert.match(t.description, /request_confirmation/);
    assert.ok(!(USER_CONFIRMED_PARAM in t.inputSchema), 'Claude never sees the flag');
    assert.equal(affectedCount('explode_entities', { handles: ['A', 'B'] }), 2);
    assert.deepEqual(existingSolidsChanged('explode_entities', { handles: ['A'] }, no), []); // the plugin decides by type
  });
});

describe('3D token cut (decided 12/11/2026): no tool renamed, parameter meanings kept', () => {
  const BUILDERS = ['draw_box', 'draw_cylinder', 'draw_sphere', 'draw_wedge', 'draw_torus', 'draw_pyramid', 'extrude', 'revolve', 'sweep', 'loft'];
  const tools = buildTools(bridge);
  const shape = (n: string) => tools.find((t) => t.name === n)!.inputSchema as Record<string, unknown>;

  it('the 10 solid builders show only the layer of the common properties', () => {
    for (const n of BUILDERS) {
      const s = shape(n);
      assert.ok('layer' in s, `${n}: layer`);
      for (const k of ['color', 'linetype', 'lineweight', 'layout']) assert.ok(!(k in s), `${n}: ${k}`);
    }
    // the 2D drawing tools keep them
    for (const k of ['layer', 'color', 'linetype', 'lineweight', 'layout']) assert.ok(k in shape('draw_line'), `draw_line: ${k}`);
    // the 3D rules say how to colour solids instead
    assert.match(THREE_D_RULES, /set_entity_props/);
  });

  it('the 3D tool names and the plan labels are unchanged (29 tools)', () => {
    assert.deepEqual(tools.filter((t) => G.get(t.name) === 'ThreeD').map((t) => t.name).sort(), threeD);
    assert.equal(threeD.length, 29);
  });

  it('the moved uses and examples are in the 3D skill, the rules stay in the tool parameters', () => {
    const md = fs.readFileSync(path.join(repo, 'skills', 've-3d-kien-truc', 'SKILL.md'), 'utf8');
    for (const needle of ['`extrude`', '`revolve`', '`sweep`', '`loft`', 'area_min', 'openings', 'set_entity_props'])
      assert.ok(md.includes(needle), needle);
    const b3 = shape('build_3d_from_plan') as Record<string, { description?: string }>;
    for (const k of ['wall_handles', 'centerline_handles', 'thickness', 'openings', 'min_thickness', 'area_min', 'on_existing', 'dry_run'])
      assert.ok((b3[k]?.description ?? '').length > 10, `build_3d_from_plan.${k} still described`);
  });
});

describe('upgrade from the original ClaudeCAD (decided 12/11/2026): old 2D sample skills need no edit', () => {
  const write = (dir: string, name: string, frontmatter: string) => {
    fs.mkdirSync(path.join(dir, name), { recursive: true });
    fs.writeFileSync(path.join(dir, name, 'SKILL.md'), `---\nname: ${name}\ndescription: x\n${frontmatter}---\n# ${name}\n`);
  };

  it('a 2D sample without a plan line (the copy the installer kept) is 2D; the user\'s own skills and plan lines are untouched', () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'cc-oldskills-'));
    try {
      for (const n of SAMPLE_2D_SKILLS) write(dir, n, ''); // old copies: no "plan:" line
      write(dir, 'khung-ten-cty', ''); // the user's own skill
      write(dir, 've-3d-kien-truc', 'plan: 3d\n');
      for (const n of SAMPLE_2D_SKILLS) assert.equal(skillPlan(dir, n), '2d', n);
      assert.equal(skillPlan(dir, 'khung-ten-cty'), undefined);
      const all = [...SAMPLE_2D_SKILLS, 'khung-ten-cty', 've-3d-kien-truc'];
      assert.deepEqual(skillsForPlan(dir, all, '3d'), ['khung-ten-cty', 've-3d-kien-truc']);
      assert.deepEqual(skillsForPlan(dir, all, '2d'), [...SAMPLE_2D_SKILLS, 'khung-ten-cty']);
      assert.deepEqual(skillsForPlan(dir, all, '2d3d'), all);
      // a plan line in the file always wins: "plan: all" loads a sample everywhere, "plan: 3d" makes it 3D-only
      write(dir, 've-mep', 'plan: all\n');
      write(dir, 've-ha-tang', 'plan: 3d\n');
      assert.equal(skillPlan(dir, 've-mep'), undefined);
      assert.equal(skillPlan(dir, 've-ha-tang'), '3d');
      assert.equal(skillPlan(dir, 'missing'), undefined);
    } finally {
      fs.rmSync(dir, { recursive: true, force: true });
    }
  });

  it('the sample list matches the shipped 2D skills and the plugin\'s built-in names', () => {
    const shipped = librarySkills(path.join(repo, 'skills'));
    const tagged2d = shipped.filter((n) => /^plan:\s*2d\s*$/m.test(fs.readFileSync(path.join(repo, 'skills', n, 'SKILL.md'), 'utf8')));
    assert.deepEqual([...SAMPLE_2D_SKILLS].sort(), tagged2d.sort());
    const store = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'Core', 'SkillStore.cs'), 'utf8');
    const builtin = /BuiltinNames\s*=\s*\{([^}]*)\}/.exec(store)![1]!.match(/"([^"]+)"/g)!.map((x) => x.slice(1, -1));
    for (const n of SAMPLE_2D_SKILLS) assert.ok(builtin.includes(n), n);
  });
});

describe('Kiểm tra độc lập fixes in the bộ cài / tích hợp part (12/11/2026)', () => {
  it('the card button sends its id, and the sidecar keeps it (C2)', async () => {
    const { parsePluginMessage } = await import('../src/ipc/protocol.js');
    const m = parsePluginMessage({ type: 'user_message', text: 'Đồng ý', confirm: 'yes', confirm_id: 'confirm-7' }) as { confirm_id?: string };
    assert.equal(m.confirm_id, 'confirm-7');
    const long = parsePluginMessage({ type: 'user_message', text: 'x', confirm: 'yes', confirm_id: 'c'.repeat(65) }) as { confirm_id?: string };
    assert.equal(long.confirm_id, undefined);
    const ctl = fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', 'ChatController.cs'), 'utf8');
    assert.ok(ctl.includes('payload["confirm_id"] = confirmId;'));
  });

  it('KT-39: make-apps-script refuses to write the filled Code.gs inside the repository', async () => {
    const { execFileSync } = await import('node:child_process');
    const crypto = await import('node:crypto');
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'cc-gs-'));
    try {
      const { privateKey } = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 });
      const pem = path.join(dir, 'k.pem');
      fs.writeFileSync(pem, privateKey.export({ type: 'pkcs8', format: 'pem' }).toString());
      const script = path.join(repo, 'activation', 'make-apps-script.mjs');
      const inRepo = path.join(repo, 'Code-test-should-not-exist.gs');
      assert.throws(() => execFileSync(process.execPath, [script, '--private-key', pem, '--email', 'a@b.c', '--out', inRepo], { stdio: 'pipe' }), /outside the repository/);
      assert.ok(!fs.existsSync(inRepo));
      const outside = path.join(dir, 'Code.gs');
      execFileSync(process.execPath, [script, '--private-key', pem, '--email', 'a@b.c', '--out', outside], { stdio: 'pipe' });
      assert.ok(!fs.readFileSync(outside, 'utf8').includes('{{'));
      // and git ignores every filled *.gs, while the template stays tracked
      const gi = fs.readFileSync(path.join(repo, '.gitignore'), 'utf8');
      assert.match(gi, /^\*\.gs$/m);
      assert.match(gi, /^\*\.pem$/m);
    } finally {
      fs.rmSync(dir, { recursive: true, force: true });
    }
  });

  it('KT-34: the 3D rules tell Claude to show the card at once for older solids (erase, break up, shape changes)', () => {
    assert.match(THREE_D_RULES, /erasing them with erase_entities or breaking them up/);
    assert.match(THREE_D_RULES, /Đồng ý button of a card/);
    assert.match(systemPromptFor('2d'), /A typed "ok" \/ "đồng ý" or the plan card's "Làm" does not count/);
  });
});
