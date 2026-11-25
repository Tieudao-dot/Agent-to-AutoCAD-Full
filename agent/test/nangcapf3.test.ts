/**
 * Nâng cấp đợt F3 (25/11/2026, docs/ban-giao/nang-cap-f3.md): roofs over L / T / U houses as one solid, materials and
 * named views (+ PNG), the new defaults (stair waist 120, sloped roof slab 120, flat roof 150).
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { affectedCount } from '../src/agent/confirmation.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';
import { MATERIAL_PRESETS, RENDER_TOOL_NAMES } from '../src/tools/render.js';
import { STAIR_ROOF } from '../src/tools/solids3.js';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const plugin = (...p: string[]) => fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', ...p), 'utf8');
const plansSrc = plugin('Core', 'Plans.cs');
const threeD = plansSrc.slice(plansSrc.indexOf('Add(ToolGroup.ThreeD,'));
const sch = (name: string) => {
  const t = buildTools(bridge).find((x) => x.name === name)!;
  return { safeParse: (v: unknown) => (t as unknown as { inputSchema: Record<string, { safeParse: (x: unknown) => { success: boolean } }> }).inputSchema && checkShape(t.inputSchema as Record<string, unknown>, v as Record<string, unknown>) };
};
function checkShape(shape: Record<string, unknown>, v: Record<string, unknown>): { success: boolean } {
  for (const [k, s] of Object.entries(shape)) {
    const r = (s as { safeParse: (x: unknown) => { success: boolean } }).safeParse(v[k]);
    if (!r.success) return { success: false };
  }
  return { success: Object.keys(v).every((k) => k in shape) };
}

describe('F3: materials and named views', () => {
  it('are 3D tools: on the 3D and Full plans, never on 2D', () => {
    for (const t of RENDER_TOOL_NAMES) {
      assert.match(threeD, new RegExp(`\\b${t}\\b`), `${t} labelled ThreeD in Plans.cs`);
      assert.match(plugin('UI', 'ToolLabels.cs'), new RegExp(`\\["${t}"\\] = "`), `${t} has a palette label`);
    }
    const all = buildTools(bridge).map((t) => t.name);
    for (const t of RENDER_TOOL_NAMES) assert.ok(all.includes(t), t);
    assert.match(systemPromptFor('3d'), /set_material/);
    assert.match(systemPromptFor('3d', '3d'), /named_view/);
    assert.doesNotMatch(systemPromptFor('2d'), /set_material|named_view/);
    // no coordinates: they stay WCS in a UCS (F1)
    assert.match(plugin('Core', 'Ucs.cs'), /"set_material", "named_view",/);
  });

  it('the presets are the plugin\'s (Core/Materials.cs)', () => {
    const src = plugin('Core', 'Materials.cs');
    for (const k of MATERIAL_PRESETS) assert.match(src, new RegExp(`Key = "${k}"`), k);
    assert.equal((src.match(/new Preset \{ Key = /g) ?? []).length, MATERIAL_PRESETS.length);
  });

  it('schemas: material on handles / layers, list, custom colour; views by action', () => {
    assert.ok(sch('set_material').safeParse({ material: 'glass', handles: ['2A'] }).success);
    assert.ok(sch('set_material').safeParse({ material: 'Gỗ lim', color: [120, 60, 30], opacity: 100, layers: ['A-DOOR-3D'] }).success);
    assert.ok(sch('set_material').safeParse({ list: true }).success);
    assert.ok(!sch('set_material').safeParse({ material: 'x', color: [300, 0, 0], handles: ['2A'] }).success);
    assert.ok(!sch('set_material').safeParse({ material: 'x', opacity: 120, handles: ['2A'] }).success);
    for (const action of ['save', 'restore', 'list', 'delete', 'image']) assert.ok(sch('named_view').safeParse({ action, name: 'Mặt tiền' }).success, action);
    assert.ok(sch('named_view').safeParse({ action: 'image', view: 'sw_iso', visual_style: 'realistic', output: 'phoi-canh.png', size: 1568 }).success);
    assert.ok(!sch('named_view').safeParse({ action: 'zoom' }).success);
    assert.ok(!sch('named_view').safeParse({ action: 'image', size: 4000 }).success);
  });

  it('changing existing objects follows the usual rules', () => {
    assert.equal(affectedCount('set_material', { material: 'glass', handles: ['A', 'B', 'C'] }), 3);
    assert.equal(affectedCount('set_material', { list: true }), 0);
    assert.equal(affectedCount('named_view', { action: 'delete', name: 'a' }), 1);
    assert.equal(affectedCount('named_view', { action: 'save', name: 'a', replace: true }), 1);
    assert.equal(affectedCount('named_view', { action: 'save', name: 'a' }), 0);
    assert.equal(affectedCount('named_view', { action: 'image' }), 0);
  });
});

describe('F3: roofs and the new defaults', () => {
  it('the L / T house is built as one roof: no "overlap" advice left', () => {
    const d = buildTools(bridge).find((t) => t.name === 'build_roof')!.description;
    assert.match(d, /right-angled L \/ T \/ U house \(one solid with valleys/);
    assert.doesNotMatch(d, /overlap at the joint|counts it twice/);
    const skill = fs.readFileSync(path.join(repo, 'skills', 've-3d-kien-truc', 'SKILL.md'), 'utf8');
    assert.doesNotMatch(skill, /chồng nhau ở chỗ nối/);
    assert.match(skill, /một\s+khối liền/);
  });

  it('defaults agreed 25/11/2026: waist 120, sloped roof 120, flat roof 150; the rest unchanged', () => {
    assert.equal(STAIR_ROOF.DefaultWaist, 120);
    assert.equal(STAIR_ROOF.DefaultSlopedRoofThickness, 120);
    assert.equal(STAIR_ROOF.DefaultFlatRoofThickness, 150);
    assert.equal(STAIR_ROOF.DefaultGap, 100);
    assert.equal(STAIR_ROOF.DefaultRiser, 160);
    assert.equal(STAIR_ROOF.DefaultTread, 280);
    assert.equal(STAIR_ROOF.DefaultWidth, 1000);
    assert.equal(STAIR_ROOF.DefaultSlopeDeg, 30);
    assert.equal(STAIR_ROOF.DefaultOverhang, 600);
    const tools = buildTools(bridge);
    assert.match(tools.find((t) => t.name === 'build_stair')!.description, /waist 120/);
    const roof = tools.find((t) => t.name === 'build_roof')!.description;
    assert.match(roof, /slab thickness default 150/);
    assert.match(roof, /slab 120 mm/);
    const skill = fs.readFileSync(path.join(repo, 'skills', 've-3d-kien-truc', 'SKILL.md'), 'utf8');
    assert.match(skill, /bản thang 120/);
    assert.match(skill, /bản mái 120/);
    assert.match(skill, /mái bằng, dày 150/);
  });
});
