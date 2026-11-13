/**
 * Nâng cấp đợt A (13/11/2026): draw_solid (one tool for the six primitives), build_3d_from_plan with other layer names
 * (asked first), columns, several storeys at once, build_railing, array_rect levels.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { z } from 'zod';
import { confirmationError, layerNameRegex, planLayersOf, unconfirmedPlanLayers, type GateState } from '../src/agent/confirmation.js';
import { THREE_D_RULES, systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';
import { RAILING } from '../src/tools/plan3d.js';
import { SOLID_ALIASES, SOLID_SHAPES } from '../src/tools/solids.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');
const src = (...parts: string[]) => fs.readFileSync(path.join(repo, ...parts), 'utf8');
const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const tools = buildTools(bridge);
const byName = (n: string) => tools.find((t) => t.name === n)!;
const ok = (n: string, p: unknown, list = tools) =>
  z.object(list.find((t) => t.name === n)!.inputSchema as z.ZodRawShape).safeParse(p).success;
const size = (n: string, list = tools) => {
  const t = list.find((x) => x.name === n)!;
  return t.description.length + JSON.stringify(z.toJSONSchema(z.object(t.inputSchema as z.ZodRawShape))).length;
};

describe('nâng cấp đợt A: draw_solid', () => {
  it('replaces the six primitives for Claude; the plugin keeps the old names for macros', () => {
    for (const a of SOLID_ALIASES) assert.ok(!tools.some((t) => t.name === a), a);
    assert.ok(byName('draw_solid'));
    const cs = src('plugin', 'ClaudeCAD.Plugin', 'Drawing', 'SolidTools.cs');
    for (const a of ['draw_solid', ...SOLID_ALIASES]) assert.match(cs, new RegExp(`Register\\("${a}"`), a);
    const core = src('plugin', 'ClaudeCAD.Plugin', 'Core', 'Solids.cs');
    for (const sh of SOLID_SHAPES) assert.match(core, new RegExp(`\\["${sh}"\\] = new\\[\\]`), sh);
  });

  it('is much smaller than the six tools it replaces (about 2k tokens less per session)', () => {
    // the six tools of 3D phase 1 measured 13/11/2026 before the merge: ~8.3k characters of description + schema
    const now = size('draw_solid');
    assert.ok(now < 4200, `draw_solid is ${now} characters`);
  });

  it('every shape takes its typical call', () => {
    assert.ok(ok('draw_solid', { shape: 'box', center: [0, 0, 1650], length: 400, width: 400, height: 3300 }));
    assert.ok(ok('draw_solid', { shape: 'pyramid', base: [0, 0], radius: 500, top_radius: 200, height: 800, rotation: 45 }));
    assert.ok(!ok('draw_solid', { shape: 'cone', base: [0, 0], radius: 1, height: 1 }));
  });

  it('the prompt and the 3D skill use draw_solid, not the old names', () => {
    for (const a of SOLID_ALIASES) {
      assert.ok(!new RegExp(`\\b${a}\\b`).test(THREE_D_RULES), `prompt: ${a}`);
      assert.ok(!new RegExp(`\\b${a}\\b`).test(src('skills', 've-3d-kien-truc', 'SKILL.md')), `skill: ${a}`);
    }
    assert.match(THREE_D_RULES, /draw_solid/);
  });
});

describe('nâng cấp đợt A: build_3d_from_plan with other layer names', () => {
  const g = (userText: string, extra: Partial<GateState> = {}): GateState => ({ userText, answersQuestion: false, ...extra });
  const call = { wall_layers: ['TUONG'], door_layers: ['CUA'], wall_height: 3300 };
  const card = 'Dựng 3D với: tường = TUONG, cửa đi = CUA?';

  it('a build from guessed layers is refused until the user approves them on a card', () => {
    assert.deepEqual(unconfirmedPlanLayers('build_3d_from_plan', call, g('dựng 3D mặt bằng này')), ['TUONG', 'CUA']);
    const err = confirmationError('build_3d_from_plan', call, g('dựng 3D mặt bằng này'));
    assert.match(err ?? '', /^Confirmation required/);
    assert.match(err ?? '', /request_confirmation/);
    assert.match(err ?? '', /TUONG, CUA/);
    // inside a batch too
    assert.ok(confirmationError('batch_execute', { calls: [{ tool: 'build_3d_from_plan', params: call }] }, g('dựng 3D')));
    // the words "tường" / "cửa", with or without marks, are not the layers TUONG / CUA
    assert.ok(confirmationError('build_3d_from_plan', call, g('dựng tường và cửa thành 3D')));
    assert.ok(confirmationError('build_3d_from_plan', call, g('dung 3d mat bang nay, tuong cao 3m3, cua cao 2m2')));
    assert.ok(confirmationError('build_3d_from_plan', call, g('dung 3d, tuong cao 3m, mai lop ton')), '"lợp" typed without marks is not "lớp"');
  });

  it('the card that names the layers unlocks them: its Đồng ý button, the plan card, or a typed yes to it', () => {
    assert.equal(confirmationError('build_3d_from_plan', call, g('Đồng ý', { confirmedByButton: true, askedText: card })), undefined);
    assert.equal(confirmationError('build_3d_from_plan', call, g('ok', { answersQuestion: true, askedText: card })), undefined);
    assert.equal(confirmationError('build_3d_from_plan', call, g('Làm theo kế hoạch.', { confirmedByButton: true, askedText: 'Dựng 3D nhà 2 tầng\nĐọc tường trên layer TUONG, cửa trên layer CUA\nDựng 2 tầng cao 3,3 m' })), undefined);
    // a yes to a card about something else approves no layer
    const other = confirmationError('build_3d_from_plan', call, g('Đồng ý', { confirmedByButton: true, askedText: 'Xóa 30 đối tượng trên layer A-TEMP?' }));
    assert.match(other ?? '', /TUONG, CUA/);
    // a card that names only some of the layers approves only those
    assert.deepEqual(unconfirmedPlanLayers('build_3d_from_plan', call, g('Đồng ý', { confirmedByButton: true, askedText: 'Dựng 3D với tường = TUONG?' })), ['CUA']);
    // a yes without any card (Claude asked in plain text) is not enough: the question goes through the card
    assert.ok(confirmationError('build_3d_from_plan', call, g('ok', { answersQuestion: true })));
    assert.ok(confirmationError('build_3d_from_plan', call, g('ok', { answersQuestion: true, askedText: '' })));
    // the card was shown but the user did not say yes (Hủy, or another request)
    assert.ok(confirmationError('build_3d_from_plan', call, g('Hủy', { askedText: card })));
    assert.ok(confirmationError('build_3d_from_plan', call, g('không, đợi đã', { answersQuestion: true, askedText: card })));
    // a typed "ok" that answers nothing
    assert.ok(confirmationError('build_3d_from_plan', call, g('ok', { askedText: card })));
  });

  it('a whole layer name must be on the card, in any letter case', () => {
    const one = (layer: string, text: string) =>
      unconfirmedPlanLayers('build_3d_from_plan', { wall_layers: [layer] }, g('Đồng ý', { confirmedByButton: true, askedText: text })).length === 0;
    assert.ok(one('TUONG', 'tường = tuong?'));
    assert.ok(one('KT_TƯỜNG', 'Tường = KT_TƯỜNG, đúng không?'));
    assert.ok(one('CUA SO', 'cửa sổ = CUA SO?'));
    assert.ok(one('A-WALL-NEW', 'tường = "A-WALL-NEW"?'));
    assert.ok(!one('CUA', 'cửa sổ = CUA-SO?'), 'CUA is not CUA-SO');
    assert.ok(!one('CUA', 'cửa = CUADI?'));
    assert.ok(!one('WALL', 'tường = A-WALL-NEW?'));
    assert.ok(!one('TUONG', 'Dựng 3D mặt bằng này?'));
    // patterns: the pattern itself or a name it covers; "*" and near-empty patterns are never approved by a text
    assert.ok(one('KT_TUONG*', 'tường = các layer KT_TUONG*?'));
    assert.ok(one('KT_TUONG*', 'tường = KT_TUONG_110 và KT_TUONG_220?'));
    assert.ok(!one('KT_TUONG*', 'tường = TUONG?'));
    assert.ok(!one('*', 'tường = mọi layer (*)?'));
    assert.ok(!one('T*', 'tường = T*?'));
    assert.equal(layerNameRegex('*'), undefined);
    assert.ok(layerNameRegex('A.WALL(1)')!.test('tường = A.WALL(1)?'), 'regex characters in a name are literal');
    assert.ok(!layerNameRegex('A.WALL')!.test('tường = AXWALL?'));
  });

  it('layers the user named themselves need no card: a message about layers that writes the name', () => {
    assert.equal(confirmationError('build_3d_from_plan', call, g('tường ở layer TUONG, cửa ở layer CUA, dựng 3D đi')), undefined);
    assert.equal(confirmationError('build_3d_from_plan', call, g('tuong o layer tuong, cua o layer cua')), undefined);
    assert.equal(confirmationError('build_3d_from_plan', call, g('Lớp TUONG là tường, lớp CUA là cửa đi')), undefined);
    // named in an earlier message of this conversation
    assert.equal(confirmationError('build_3d_from_plan', call, g('dựng 3D đi', { userTexts: ['tường ở layer TUONG, cửa ở layer CUA nhé', 'dựng 3D đi'] })), undefined);
    assert.deepEqual(unconfirmedPlanLayers('build_3d_from_plan', call, g('dựng 3D đi', { userTexts: ['tường ở layer TUONG', 'dựng 3D đi'] })), ['CUA']);
    // a pattern is named when the user wrote a name it covers
    assert.equal(confirmationError('build_3d_from_plan', { wall_layers: ['KT_TUONG*'] }, g('tường là các layer KT_TUONG')), undefined);
  });

  it('approved layers stay approved in the conversation; reading and the default layers never ask', () => {
    assert.equal(confirmationError('build_3d_from_plan', call, g('dựng tầng 2', { planLayersOk: new Set(['TUONG', 'CUA']) })), undefined);
    assert.deepEqual(unconfirmedPlanLayers('build_3d_from_plan', { ...call, window_layers: ['CUASO'] }, g('dựng tầng 2', { planLayersOk: new Set(['TUONG', 'CUA']) })), ['CUASO']);
    assert.equal(confirmationError('build_3d_from_plan', { ...call, dry_run: true }, g('dựng 3D')), undefined);
    assert.equal(confirmationError('build_3d_from_plan', { wall_layers: ['A-WALL'], column_layers: ['a-cols'] }, g('dựng 3D')), undefined);
    assert.equal(confirmationError('build_3d_from_plan', { wall_height: 3300 }, g('dựng 3D')), undefined);
    assert.deepEqual(planLayersOf('batch_execute', { calls: [{ tool: 'build_3d_from_plan', params: call }, { tool: 'build_3d_from_plan', params: { wall_layers: ['X'], dry_run: true } }] }), ['TUONG', 'CUA']);
  });

  it('the plugin reports the guess and the tool tells Claude to ask', () => {
    const cs = src('plugin', 'ClaudeCAD.Plugin', 'Drawing', 'PlanTo3dTools.cs');
    assert.match(cs, /report\["layer_guess"\]/);
    assert.match(byName('build_3d_from_plan').description, /layer_guess/);
    assert.match(byName('build_3d_from_plan').description, /request_confirmation/);
    assert.match(THREE_D_RULES, /request_confirmation[\s\S]{0,120}TUONG/);
  });
});

describe('nâng cấp đợt A: columns and several storeys', () => {
  it('build_3d_from_plan takes the new parameters', () => {
    assert.ok(ok('build_3d_from_plan', { storeys: 3, storey_height: 3300, slab_thickness: 120, roof_slab: true }));
    assert.ok(ok('build_3d_from_plan', { columns: 'auto', column_layers: ['COT'], column_handles: ['2F'], column_layer_3d: 'A-COLS-3D' }));
    assert.ok(!ok('build_3d_from_plan', { storeys: 0 }));
    assert.ok(!ok('build_3d_from_plan', { storeys: 51 }));
    assert.ok(!ok('build_3d_from_plan', { columns: 'all' }));
  });

  it('the prompt explains storeys as storey_height − slab (L24) and the skill keeps the base_z rule', () => {
    assert.match(THREE_D_RULES, /storeys 3, storey_height 3300/);
    const md = src('skills', 've-3d-kien-truc', 'SKILL.md');
    assert.match(md, /"storeys": 3, "storey_height": 3300/);
    assert.match(md, /`base_z`/);
  });
});

describe('nâng cấp đợt A: build_railing', () => {
  it('is a 3D tool with documented defaults that match the plugin', () => {
    const t = byName('build_railing');
    assert.match(t.description, /1100 mm high/);
    const cs = src('plugin', 'ClaudeCAD.Plugin', 'Core', 'Railing.cs');
    for (const [k, v] of Object.entries(RAILING)) {
      if (typeof v === 'number') assert.match(cs, new RegExp(`${k} = ${v}[;,]`), `${k} = ${v}`);
      else assert.match(cs, new RegExp(`${k} = "${v}"`), k);
    }
    assert.ok(ok('build_railing', { path_handle: '2F4A' }));
    assert.ok(ok('build_railing', { path_points: [[0, 0], [4000, 0], [4000, 1500]], height: 1200, infill: 'panel', base_z: 3300 }));
    assert.ok(!ok('build_railing', { path_points: [[0, 0]] }));
    assert.ok(!ok('build_railing', { path_handle: '2F', height: 100 }));
  });

  it('is hidden on a 2D machine', () => {
    const two = buildTools(bridge, ['draw_polyline', 'array_rect'], '2d').map((t) => t.name);
    assert.ok(!two.includes('build_railing'));
  });
});

describe('nâng cấp đợt A: array_rect levels', () => {
  it('Full and 3D see levels, 2D does not (the plugin refuses levels > 1 without 3D)', () => {
    assert.ok(ok('array_rect', { handles: ['1A'], rows: 2, cols: 3, row_spacing: 6000, col_spacing: 4000, levels: 3, level_spacing: 3300 }));
    const three = buildTools(bridge, ['array_rect'], '3d');
    assert.ok(ok('array_rect', { handles: ['1A'], rows: 1, cols: 1, row_spacing: 0, col_spacing: 0, levels: 3, level_spacing: 3300 }, three));
    const two = buildTools(bridge, ['array_rect'], '2d');
    const shape = two[0]!.inputSchema as Record<string, unknown>;
    assert.ok(!('levels' in shape) && !('level_spacing' in shape));
    const plans = src('plugin', 'ClaudeCAD.Plugin', 'Core', 'Plans.cs');
    assert.match(plans, /\["array_rect"\] = "levels"/);
  });

  it('the 3D plan prompt still names only tools of the plan', () => {
    assert.ok(!systemPromptFor('2d').includes('build_railing'));
    assert.ok(systemPromptFor('3d').includes('build_railing'));
  });
});
