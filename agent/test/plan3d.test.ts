/**
 * 3D phase 3 (docs/ban-giao/ve-3d-dot-3.md): build_3d_from_plan, section_view, projection_view — declarations, the
 * 3D plan only, limits shared with the plugin, the confirmation gate (they only create), the system prompt and the
 * "3D kiến trúc" skill that a 2D machine does not load.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { z } from 'zod';
import { affectedCount, confirmationError, createdHandles, existingSolidsChanged } from '../src/agent/confirmation.js';
import { skillPlan, skillsForPlan, prepareSkillsPlugin } from '../src/agent/skills.js';
import { THREE_D_RULES, systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';
import { PLAN3D_LAYERS, PLAN3D_LIMITS, PLAN3D_TOOL_NAMES } from '../src/tools/plan3d.js';
import { SOLID_TOOL_NAMES } from '../src/tools/solids.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');
const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: { handles: ['1A'] } }), status: () => {} };
const tools = buildTools(bridge);
const byName = (n: string) => tools.find((t) => t.name === n)!;
const ok = (n: string, p: unknown) => z.object(byName(n).inputSchema as z.ZodRawShape).safeParse(p).success;
const src = (...parts: string[]) => fs.readFileSync(path.join(repo, ...parts), 'utf8');

describe('3D phase 3 tools', () => {
  it('are declared, in the 3D list, labelled ThreeD in the plugin, and a 2D machine does not load them', () => {
    const plans = src('plugin', 'ClaudeCAD.Plugin', 'Core', 'Plans.cs');
    for (const n of PLAN3D_TOOL_NAMES) {
      assert.ok(byName(n), n);
      assert.ok((SOLID_TOOL_NAMES as readonly string[]).includes(n), n);
      assert.match(plans, new RegExp(`"[^"]*\\b${n}\\b[^"]*"\\);\\s*return d;`), `${n} in the ThreeD group (last Add of Plans.cs)`);
    }
    const twoD = tools.map((t) => t.name).filter((n) => !(SOLID_TOOL_NAMES as readonly string[]).includes(n));
    const kept = buildTools(bridge, twoD).map((t) => t.name);
    for (const n of PLAN3D_TOOL_NAMES) assert.ok(!kept.includes(n), n);
  });

  it('schemas accept typical calls and reject bad ones', () => {
    assert.ok(ok('build_3d_from_plan', { wall_height: 3300 }));
    assert.ok(ok('build_3d_from_plan', { dry_run: true, wall_layers: ['KT_TUONG*'], area_min: [0, 0], area_max: [20000, 15000] }));
    assert.ok(ok('build_3d_from_plan', { centerline_handles: ['2F'], thickness: 220, openings: [{ kind: 'door', at: [1000, 0] }, { kind: 'window', at: [3000, 0], width: 1200, sill: 900, height: 1500 }] }));
    assert.ok(ok('build_3d_from_plan', { slab: 'bbox', slab_thickness: 0, base_z: 3300, on_existing: 'add' }));
    assert.ok(!ok('build_3d_from_plan', { wall_height: 50 }));
    assert.ok(!ok('build_3d_from_plan', { slab: 'outline' }));
    assert.ok(!ok('build_3d_from_plan', { openings: [{ kind: 'gate', at: [0, 0] }] }));
    assert.ok(!ok('build_3d_from_plan', { thickness: 5000, centerline_handles: ['2F'] }));
    assert.ok(ok('section_view', { handles: ['2F4A'], line: [[0, 1000], [5000, 1000]], show_behind: true }));
    assert.ok(ok('section_view', { handles: ['2F4A'], point: [0, 0, 1000], plane: 'xy', hatch: 'AR-CONC', hatch_scale: 20 }));
    assert.ok(ok('section_view', { handles: ['2F4A'], point: [0, 0, 1000], plane: 'xy', hatch: false }));
    assert.ok(!ok('section_view', { handles: ['2F4A'], line: [[0, 0]] }));
    assert.ok(!ok('section_view', { handles: [] }));
    assert.ok(ok('projection_view', { handles: ['2F4A', '2F4B'], view: 'front' }));
    assert.ok(ok('projection_view', { handles: ['2F4A'], direction: [1, -1, 0], method: 'edges', position: [30000, 0] }));
    assert.ok(!ok('projection_view', { handles: ['2F4A'], view: 'south' }));
  });

  it('limits, defaults and layers match the plugin (Core/FloorPlan.cs)', () => {
    const cs = src('plugin', 'ClaudeCAD.Plugin', 'Core', 'FloorPlan.cs');
    const alias: Record<string, string> = { MaxSectionSolids: 'MaxSolids' };
    for (const [k, v] of Object.entries(PLAN3D_LIMITS)) assert.match(cs, new RegExp(`\\b${alias[k] ?? k} = ${v}[,;]`), k);
    for (const l of [...PLAN3D_LAYERS.wall, ...PLAN3D_LAYERS.door, ...PLAN3D_LAYERS.window, ...PLAN3D_LAYERS.slab]) assert.ok(cs.includes(`"${l}"`), l);
    for (const k of ['wall3d', 'slab3d', 'section', 'sectionHatch', 'sectionBehind', 'elevation'] as const) assert.ok(cs.includes(`"${PLAN3D_LAYERS[k]}"`), k);
  });

  it('they only create: no confirmation, their handles count as created in the turn', () => {
    const g = { userText: 'dựng 3D', answersQuestion: false };
    for (const n of PLAN3D_TOOL_NAMES) {
      assert.equal(affectedCount(n, { handles: Array.from({ length: 50 }, (_, i) => `A${i}`) }), 0, n);
      assert.deepEqual(existingSolidsChanged(n, { handles: ['2F'] }, g), [], n);
      assert.equal(confirmationError(n, { handles: Array.from({ length: 50 }, (_, i) => `A${i}`) }, g), undefined, n);
    }
    assert.deepEqual(createdHandles('build_3d_from_plan', {}, { handles: ['3A', '3B'] }), ['3A', '3B']);
    // the walls just built can be cut in the same turn without a card; slicing them needs none either
    const created = new Set(createdHandles('build_3d_from_plan', {}, { handles: ['3A'] }));
    assert.equal(confirmationError('slice_solid', { handle: '3A', point: [0, 0, 1000], plane: 'xy' }, { ...g, created }), undefined);
    assert.match(confirmationError('slice_solid', { handle: '2F', point: [0, 0, 1000], plane: 'xy' }, { ...g, created })!, /Confirmation required/);
  });

  it('the 3D system prompt explains when to use them; a 2D machine does not get it', () => {
    for (const needle of ['build_3d_from_plan', 'dry_run: true', 'open_gaps', 'skipped', 'never guess them', 'already built', 'on_existing', 'section_view', 'projection_view', 'cut_area_m2'])
      assert.ok(THREE_D_RULES.includes(needle), needle);
    // KT-14 / KT-16 (independent check): never propose erasing another storey; base_z is mm
    assert.ok(THREE_D_RULES.includes('never propose erasing another storey or the storey below'));
    assert.ok(!THREE_D_RULES.includes('erase them first'));
    assert.match(byName('build_3d_from_plan').inputSchema.base_z!.description ?? '', /in mm/);
    assert.match(byName('build_3d_from_plan').description, /it only reads/);
    assert.ok(!systemPromptFor('2d').includes('build_3d_from_plan'));
    assert.ok(systemPromptFor('3d').includes('build_3d_from_plan'));
    assert.ok(systemPromptFor('2d3d').includes('section_view'));
    assert.match(systemPromptFor('3d'), /sections and projections with their own outline and hatch/);
  });
});

describe('3D kiến trúc skill', () => {
  const skillsDir = path.join(repo, 'skills');

  it('is a 3D-only skill with the layers and the self-check of build_3d_from_plan', () => {
    assert.equal(skillPlan(skillsDir, 've-3d-kien-truc'), '3d');
    // the 2D samples are tagged "plan: 2d" since 12/11/2026 (the owner: a 3D machine does not load them)
    assert.equal(skillPlan(skillsDir, 've-kien-truc'), '2d');
    const md = src('skills', 've-3d-kien-truc', 'SKILL.md');
    for (const needle of ['A-WALL-3D', 'A-SLAB-3D', 'dry_run', 'wall_height', 'door_height', 'window_sill', 'window_height', 'slab_thickness', 'capture_view', 'section_view', 'projection_view'])
      assert.ok(md.includes(needle), needle);
  });

  it('a 2D machine does not load it; 3D / Full / an older plugin do', () => {
    const all = ['ve-3d-kien-truc', 've-kien-truc', 've-mep'];
    assert.deepEqual(skillsForPlan(skillsDir, all, '2d'), ['ve-kien-truc', 've-mep']);
    // changed 12/11/2026 (owner): the 3D machine leaves out the 2D samples ("plan: 2d"), it keeps the 3D one
    assert.deepEqual(skillsForPlan(skillsDir, all, '3d'), ['ve-3d-kien-truc']);
    assert.deepEqual(skillsForPlan(skillsDir, all, '2d3d'), all);
    assert.deepEqual(skillsForPlan(skillsDir, all, undefined), all);
  });

  it('prepareSkillsPlugin leaves it out on a 2D machine even when the chat uses it', () => {
    const work = fs.mkdtempSync(path.join(os.tmpdir(), 'cc-plan3d-'));
    try {
      const two = prepareSkillsPlugin(skillsDir, work, ['ve-3d-kien-truc', 've-kien-truc'], '2d')!;
      assert.deepEqual(two.skills, ['ve-kien-truc']);
      assert.ok(!fs.existsSync(path.join(two.pluginDir, 'skills', 've-3d-kien-truc')));
      const full = prepareSkillsPlugin(skillsDir, work, ['ve-3d-kien-truc', 've-kien-truc'], '2d3d')!;
      assert.deepEqual(full.skills, ['ve-3d-kien-truc', 've-kien-truc']);
    } finally {
      fs.rmSync(work, { recursive: true, force: true });
    }
  });

  it('a "plan: 3d" line with quotes, CRLF or a BOM is read; a user skill without it is for every plan', () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'cc-skillplan-'));
    try {
      fs.mkdirSync(path.join(dir, 'a'));
      fs.writeFileSync(path.join(dir, 'a', 'SKILL.md'), '﻿---\r\nname: a\r\nplan: "3D"\r\ndescription: x\r\n---\r\n# A\r\n');
      fs.mkdirSync(path.join(dir, 'b'));
      fs.writeFileSync(path.join(dir, 'b', 'SKILL.md'), '---\nname: b\ndescription: plan: 3d in the text only\n---\nplan: 3d\n');
      fs.mkdirSync(path.join(dir, 'c'));
      fs.writeFileSync(path.join(dir, 'c', 'SKILL.md'), '---\nname: c\nplan: 2D\ndescription: x\n---\n');
      fs.mkdirSync(path.join(dir, 'd'));
      fs.writeFileSync(path.join(dir, 'd', 'SKILL.md'), '---\nname: d\nplan: 4d\ndescription: an unknown plan is every plan\n---\n');
      assert.equal(skillPlan(dir, 'a'), '3d');
      assert.equal(skillPlan(dir, 'b'), undefined);
      assert.equal(skillPlan(dir, 'c'), '2d');
      assert.equal(skillPlan(dir, 'd'), undefined);
      assert.deepEqual(skillsForPlan(dir, ['a', 'b', 'c', 'd'], '3d'), ['a', 'b', 'd']);
      assert.deepEqual(skillsForPlan(dir, ['a', 'b', 'c', 'd'], '2d'), ['b', 'c', 'd']);
      assert.deepEqual(skillsForPlan(dir, ['a', 'b', 'c', 'd'], '2d3d'), ['a', 'b', 'c', 'd']);
      assert.equal(skillPlan(dir, 'missing'), undefined);
    } finally {
      fs.rmSync(dir, { recursive: true, force: true });
    }
  });
});

describe('3D phase 3 test & fix (docs/ban-giao/ve-3d-test-3.md)', () => {
  it('build_3d_from_plan tells Claude what the plugin now does with print-width polylines, earlier solids and given openings', () => {
    const d = byName('build_3d_from_plan').description;
    // since 12/11/2026 (token cut, owner) the print-width rule is told on the min_thickness parameter, not in the description
    const minT = (byName('build_3d_from_plan').inputSchema as Record<string, { description?: string }>).min_thickness!.description ?? '';
    assert.match(minT, /width below it is a print line weight, read as a face line/);
    assert.match(d, /existing_solids/);
    const plugin = src('plugin', 'ClaudeCAD.Plugin', 'Drawing', 'PlanTo3dTools.cs');
    assert.match(plugin, /report\["existing_solids"\]/);
    assert.match(plugin, /WidePolylineRole/);
    const core = src('plugin', 'ClaudeCAD.Plugin', 'Core', 'FloorPlan.cs');
    assert.match(core, /runs past the end of wall/);
    assert.match(core, /meet: move it to the middle of the opening in one wall/);
  });
});
