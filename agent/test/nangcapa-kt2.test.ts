/**
 * Sửa sau kiểm tra độc lập nâng cấp (13/11/2026, docs/ban-giao/kiem-tra-doc-lap-nang-cap.md): KT2-10 and KT2-22 of
 * nâng cấp đợt A on the assistant's side (KT2-11 and KT2-21 are plugin fixes, tested in NangCapAKt2Tests.cs).
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { THREE_D_RULES, systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');
const src = (...parts: string[]) => fs.readFileSync(path.join(repo, ...parts), 'utf8');
const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const describeOf = (tool: string, param: string, plan?: string) => {
  const t = buildTools(bridge, undefined, plan).find((x) => x.name === tool)!;
  return (t.inputSchema as Record<string, { description?: string }>)[param]?.description ?? '';
};

describe('sửa sau kiểm tra độc lập nâng cấp: đợt A', () => {
  it('KT2-10: level_spacing of array_rect is mm and the plugin converts it; rows and columns are untouched', () => {
    assert.match(describeOf('array_rect', 'level_spacing'), /in mm, converted to the drawing's units/);
    assert.match(describeOf('array_rect', 'level_spacing'), /3300 = 3\.3 in a metres drawing/);
    const cs = src('plugin', 'ClaudeCAD.Plugin', 'Drawing', 'ModifyTools.cs');
    assert.match(cs, /double ls = FloorPlanLogic\.ToDrawingUnits\(p\.Num\("level_spacing", 0\), Convert\.ToInt32\(ctx\.Db\.Insunits\)\);/);
    assert.match(cs, /double rs = p\.Num\("row_spacing", 0\);/);
    // a 2D machine still does not see the 3D parameters
    assert.equal(describeOf('array_rect', 'level_spacing', '2d'), '');
  });

  it('KT2-22: a layer only guessed as a railing is asked about before build_railing', () => {
    assert.match(THREE_D_RULES, /build_railing along a polyline the user names or picks/);
    assert.match(THREE_D_RULES, /a layer only guessed as a railing: ask first/);
    assert.ok(!systemPromptFor('2d').includes('build_railing'));
    const cs = src('plugin', 'ClaudeCAD.Plugin', 'Drawing', 'PlanTo3dTools.cs');
    assert.ok(!cs.includes('"build_railing path_handle"'), 'the report no longer says to use the guessed layer');
    assert.match(src('plugin', 'ClaudeCAD.Plugin', 'Core', 'FloorPlan.cs'), /ask the user first \(request_confirmation naming this layer\)/);
  });
});
