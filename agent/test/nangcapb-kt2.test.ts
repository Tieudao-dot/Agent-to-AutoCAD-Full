/**
 * Đợt B, sửa sau kiểm tra độc lập nâng cấp (docs/ban-giao/kiem-tra-doc-lap-nang-cap.md): KT2-14 (gói không 2D không được
 * mời chép thuộc tính chữ / kích thước), KT2-25 (vùng kéo giãn tối đa 1000 đỉnh như plugin). KT2-03, 15, 23, 24:
 * plugin/ClaudeCAD.Core.Tests/NangCapBKt2Tests.cs.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { z } from 'zod';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const toolOf = (name: string, plan?: string) => buildTools(bridge, undefined, plan).find((t) => t.name === name)!;
const schemaOf = (name: string, plan?: string) => z.object(toolOf(name, plan).inputSchema as z.ZodRawShape);

describe('đợt B › KT2-14: match_properties by plan', () => {
  it('without 2D the text / dimension properties are not offered', () => {
    const s = schemaOf('match_properties', '3d');
    assert.ok(s.safeParse({ source: 'A1', handles: ['B2'], properties: ['layer', 'color', 'thickness'] }).success);
    assert.ok(!s.safeParse({ source: 'A1', handles: ['B2'], properties: ['text'] }).success);
    assert.ok(!s.safeParse({ source: 'A1', handles: ['B2'], properties: ['dimension'] }).success);
    assert.ok(s.safeParse({ source: 'A1', handles: ['B2'] }).success); // default: all the general ones
    const d = toolOf('match_properties', '3d').description;
    assert.doesNotMatch(d, /dimension/);
    assert.doesNotMatch(d, /text \(style/);
    assert.match(d, /layer, color, linetype, lineweight, linetype_scale, transparency, thickness\. Default: all\./);
    assert.match(d, /DESTRUCTIVE/); // the confirmation sentence stays
  });

  it('2D and Full keep them, word for word as before', () => {
    for (const plan of ['2d', '2d3d', undefined]) {
      const s = schemaOf('match_properties', plan);
      assert.ok(s.safeParse({ source: 'A1', handles: ['B2'], properties: ['text', 'dimension', 'layer'] }).success, String(plan));
      assert.match(toolOf('match_properties', plan).description,
        /thickness, text \(style, height: text to text\) and dimension \(style: dimension to dimension\)\. Default: all \(text \/ dimension only on plans with 2D\)\./);
    }
  });
});

describe('đợt B › KT2-25: stretch_entities polygon', () => {
  const poly = (n: number) => Array.from({ length: n }, (_, i) => [Math.cos(i), Math.sin(i)]);

  it('at most 1000 points, like the plugin', () => {
    const s = schemaOf('stretch_entities', '2d');
    assert.ok(s.safeParse({ polygon: poly(3), displacement: [10, 0] }).success);
    assert.ok(s.safeParse({ polygon: poly(1000), displacement: [10, 0] }).success);
    assert.ok(!s.safeParse({ polygon: poly(1001), displacement: [10, 0] }).success);
    assert.ok(!s.safeParse({ polygon: poly(2), displacement: [10, 0] }).success);
  });
});
