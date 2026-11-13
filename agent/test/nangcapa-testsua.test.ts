/**
 * Test & sửa nâng cấp đợt A (13/11/2026, docs/ban-giao/nang-cap-a-test.md): the bugs found in the review and the
 * edges the first tests did not cover (layer gate work-arounds, odd layer names, the 3D card rule with the new tools).
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  confirmationError, createdHandles, layerNameRegex, planLayersOf, unconfirmedPlanLayers, type GateState,
} from '../src/agent/confirmation.js';
import { timeoutFor } from '../src/ipc/rpc.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';
import { SOLID_ALIASES } from '../src/tools/solids.js';

const g = (userText: string, more: Partial<GateState> = {}): GateState => ({ userText, answersQuestion: false, ...more });
const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };

describe('test & sửa đợt A: the layer gate cannot be walked around (NA-1)', () => {
  it('a layer given as one string (the plugin reads it as a list) is gated like an array', () => {
    for (const k of ['wall_layers', 'door_layers', 'window_layers', 'slab_layers', 'column_layers']) {
      assert.deepEqual(planLayersOf('build_3d_from_plan', { [k]: 'TUONG' }), ['TUONG'], k);
      assert.match(confirmationError('build_3d_from_plan', { [k]: 'TUONG' }, g('dựng 3D')) ?? '', /^Confirmation required/, k);
    }
    // inside a batch no schema turns the string down
    const batch = { calls: [{ tool: 'create_layer', params: { name: 'X' } }, { tool: 'build_3d_from_plan', params: { wall_layers: 'TUONG', door_layers: ['CUA'] } }] };
    assert.deepEqual(planLayersOf('batch_execute', batch), ['TUONG', 'CUA']);
    assert.match(confirmationError('batch_execute', batch, g('ok', { answersQuestion: true })) ?? '', /TUONG, CUA/);
    // a default layer as a string is still free, an empty one is nothing
    assert.equal(confirmationError('build_3d_from_plan', { wall_layers: 'a-wall', slab_layers: ' ' }, g('dựng 3D')), undefined);
  });

  it('only a real dry_run reads without asking', () => {
    const call = { wall_layers: ['TUONG'] };
    assert.equal(confirmationError('build_3d_from_plan', { ...call, dry_run: true }, g('dựng 3D')), undefined);
    for (const dry of ['true', 1, 'yes', false, null]) assert.ok(confirmationError('build_3d_from_plan', { ...call, dry_run: dry }, g('dựng 3D')), String(dry));
  });

  it('a yes to the 3D card of other solids, or a typed yes with no card, opens no layer', () => {
    const call = { wall_layers: ['TUONG'] };
    assert.ok(confirmationError('build_3d_from_plan', call, g('Đồng ý', { confirmedByButton: true, cardConfirmed: new Set(['1A2']), askedText: 'Trừ khối 1A2 khỏi tường?' })));
    assert.ok(confirmationError('build_3d_from_plan', call, g('đồng ý', { answersQuestion: true, askedText: '' })));
    // a card that names the layer but was answered with Hủy / another request
    assert.ok(confirmationError('build_3d_from_plan', call, g('Không, hủy thao tác đó.', { answersQuestion: true, askedText: 'Dựng 3D với tường = TUONG?' })));
    assert.ok(confirmationError('build_3d_from_plan', call, g('dựng cao 3 m thôi', { answersQuestion: true, askedText: 'Dựng 3D với tường = TUONG?' })));
  });

  it('other tools with a layer parameter are not touched by the gate', () => {
    assert.equal(confirmationError('build_railing', { path_points: [[0, 0], [3000, 0]], layer: 'LANCAN' }, g('làm lan can')), undefined);
    assert.equal(confirmationError('draw_solid', { shape: 'box', length: 1, width: 1, height: 1, layer: 'TUONG' }, g('vẽ hộp')), undefined);
    assert.deepEqual(planLayersOf('section_view', { wall_layers: ['TUONG'] }), []);
  });
});

describe('test & sửa đợt A: layer names with odd characters', () => {
  const named = (layer: string, text: string) => layerNameRegex(layer)?.test(text.normalize('NFC')) ?? false;

  it('names with spaces, dots, brackets, $ and Vietnamese marks match as written, in any case', () => {
    assert.ok(named('CUA SO', 'cửa sổ = CUA SO, tường = TUONG'));
    assert.ok(named('KT.TUONG(220)', 'tường = kt.tuong(220)?'));
    assert.ok(named('XREF$0$TUONG', 'tường = XREF$0$TUONG'));
    assert.ok(named('Tường 220', 'Dựng 3D với: tường = TƯỜNG 220?'));
    assert.ok(named('Tường', 'tường = Tường'.normalize('NFC')), 'a name typed with combining marks');
    assert.ok(named('TUONG', 'tường = `TUONG`, cửa = "CUA".'));
    // regex characters in a name are plain text
    assert.ok(!named('A.WALL', 'tường = AXWALL'));
    assert.ok(!named('TUONG+', 'tường = TUONGG'));
    assert.ok(named('[TUONG]', 'tường = [TUONG]'));
  });

  it('a name is a whole word: part of a longer name does not count', () => {
    for (const text of ['TUONG-220', 'KT_TUONG', 'TUONG2', 'TUONG$X', 'XTUONG']) assert.ok(!named('TUONG', `tường = ${text}`), text);
    assert.ok(!named('CUA', 'cửa sổ = CUA-SO'));
    assert.ok(named('CUA', 'cửa = CUA, cửa sổ = CUA-SO'));
  });

  it('patterns: the card may write the pattern or a name it covers; too wide patterns are never approved', () => {
    assert.ok(named('TUONG*', 'tường = TUONG-220'));
    assert.ok(named('TUONG*', 'tường = TUONG*'));
    assert.ok(named('KT?TUONG', 'tường = KT_TUONG'));
    assert.ok(!named('TUONG*', 'tường = KT_TUONG'));
    for (const wide of ['*', '**', '?', 'T*', 'A-*', '*?*', '??']) {
      assert.equal(layerNameRegex(wide), undefined, wide);
      assert.deepEqual(unconfirmedPlanLayers('build_3d_from_plan', { wall_layers: [wide] }, g('Đồng ý', { confirmedByButton: true, askedText: `Dựng 3D với tường = ${wide} (mọi layer)?` })), [wide]);
    }
  });

  it('the user naming a layer needs a message about layers; the approved set is per name', () => {
    const call = { wall_layers: ['TUONG'], door_layers: ['CUA'] };
    assert.deepEqual(unconfirmedPlanLayers('build_3d_from_plan', call, g('x', { userTexts: ['tường nằm ở layer TUONG nhé', 'dựng đi'] })), ['CUA']);
    assert.deepEqual(unconfirmedPlanLayers('build_3d_from_plan', call, g('x', { userTexts: ['lớp TUONG là tường, lớp CUA là cửa đi'] })), []);
    assert.deepEqual(unconfirmedPlanLayers('build_3d_from_plan', call, g('x', { planLayersOk: new Set(['CUA']) })), ['TUONG']);
    assert.deepEqual(unconfirmedPlanLayers('build_3d_from_plan', { wall_layers: ['tuong'] }, g('x', { planLayersOk: new Set(['TUONG']) })), []);
  });
});

describe('test & sửa đợt A: the 3D card rule with the new tools', () => {
  const pre = new Set<string>();
  it('solids built this turn by draw_solid / build_railing / build_3d_from_plan / array_rect levels need no card', () => {
    const created = new Set([
      ...createdHandles('draw_solid', { shape: 'box' }, { handle: 'a1' }),
      ...createdHandles('build_railing', { path_points: [] }, { handle: 'b1', handles: ['B1', 'b2'] }),
      ...createdHandles('build_3d_from_plan', { storeys: 2 }, { handles: ['c1', 'c2', 'c3'], levels: [] }),
      ...createdHandles('array_rect', { handles: ['A1'], levels: 3, level_spacing: 3300 }, { handles: ['d1', 'd2'] }),
      ...createdHandles('batch_execute', { calls: [{ tool: 'draw_solid', params: { shape: 'cylinder' } }] }, { results: [{ ok: true, result: { handle: 'e1' } }] }),
    ]);
    assert.deepEqual([...created].sort(), ['A1', 'B1', 'B2', 'C1', 'C2', 'C3', 'D1', 'D2', 'E1']);
    const st = g('đục lỗ cửa', { created, touched: pre });
    assert.equal(confirmationError('solid_subtract', { target: 'C1', subtract: ['A1'] }, st), undefined);
    assert.equal(confirmationError('solid_union', { handles: ['B1', 'D1', 'E1'] }, st), undefined);
  });

  it('a solid from before the turn still needs the Đồng ý of a card with its handle, whatever layers were approved', () => {
    const created = new Set(['A1']);
    const layersOk = { planLayersOk: new Set(['TUONG']), askedText: 'Dựng 3D với tường = TUONG?' };
    // typed yes, and the button of the layer card (no handles): both refused
    assert.match(confirmationError('solid_subtract', { target: 'F00', subtract: ['A1'] }, g('ok', { answersQuestion: true, created, ...layersOk })) ?? '', /^Confirmation required/);
    assert.match(confirmationError('solid_subtract', { target: 'F00', subtract: ['A1'] }, g('Đồng ý', { confirmedByButton: true, cardConfirmed: new Set(), created, ...layersOk })) ?? '', /^Confirmation required/);
    assert.equal(confirmationError('solid_subtract', { target: 'F00', subtract: ['A1'] }, g('Đồng ý', { confirmedByButton: true, cardConfirmed: new Set(['F00']), created })), undefined);
    // the building tools themselves never count as changing objects
    for (const [tool, p] of [['draw_solid', { shape: 'box' }], ['build_railing', { path_handle: 'F00' }], ['build_3d_from_plan', { wall_handles: ['F00'], column_handles: ['F01'] }]] as const) {
      assert.equal(confirmationError(tool, p, g('làm đi')), undefined, tool);
    }
  });

  it('array_rect of more than 20 existing objects is a copy: no confirmation, with or without levels', () => {
    const hs = Array.from({ length: 30 }, (_, i) => `A${i}`);
    assert.equal(confirmationError('array_rect', { handles: hs, rows: 1, cols: 1, levels: 3, level_spacing: 3300 }, g('nhân 3 tầng')), undefined);
  });
});

describe('test & sửa đợt A: what each plan sees, and time-outs', () => {
  it('a 2D machine sees no array_rect levels; 3D and Full see them, and the new tools once, never the old names', () => {
    const arr2 = buildTools(bridge, undefined, '2d').find((t) => t.name === 'array_rect')!;
    assert.ok(!('levels' in arr2.inputSchema) && !('level_spacing' in arr2.inputSchema));
    for (const plan of ['3d', '2d3d']) {
      const list = buildTools(bridge, undefined, plan);
      for (const n of ['draw_solid', 'build_railing', 'build_3d_from_plan']) assert.equal(list.filter((t) => t.name === n).length, 1, `${plan} ${n}`);
      for (const a of SOLID_ALIASES) assert.ok(!list.some((t) => t.name === a), `${plan} ${a}`);
      const arr = list.find((t) => t.name === 'array_rect')!;
      assert.ok('levels' in arr.inputSchema && 'level_spacing' in arr.inputSchema, plan);
    }
    // the plugin's list for a 2D machine (plan_tools) has none of them: only what it allows is built
    const two = buildTools(bridge, ['array_rect', 'draw_line'], '2d').map((t) => t.name);
    assert.deepEqual(two.sort(), ['array_rect', 'draw_line']);
  });

  it('a real build waits 120 s, alone or in a batch; a dry run and draw_solid keep the normal time', () => {
    assert.equal(timeoutFor('build_railing', {}, 30_000), 120_000);
    assert.equal(timeoutFor('build_3d_from_plan', { storeys: 5 }, 30_000), 120_000);
    assert.equal(timeoutFor('build_3d_from_plan', { dry_run: true }, 30_000), 30_000);
    assert.equal(timeoutFor('draw_solid', { shape: 'box' }, 30_000), 30_000);
    assert.equal(timeoutFor('batch_execute', { calls: [{ tool: 'build_railing', params: {} }] }, 30_000), 120_000);
    assert.ok(timeoutFor('batch_execute', { calls: [{ tool: 'build_3d_from_plan', params: { dry_run: true } }] }, 30_000) < 120_000);
  });
});
