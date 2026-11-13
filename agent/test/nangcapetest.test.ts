/**
 * Test & sửa đợt E (13/11/2026, docs/ban-giao/nang-cap-e-test.md): build_stair keeps the walking direction when it is
 * fitted into a stair well (NE-1), so Claude may pass direction with well_handle / well_points.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { z } from 'zod';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };

describe('test & sửa đợt E', () => {
  it('build_stair: the well fitting keeps direction (described, and accepted with a well)', () => {
    const t = buildTools(bridge).find((x) => x.name === 'build_stair')!;
    assert.match(t.description, /fitted into the well \(keeping direction when given\)/);
    const sch = z.object(t.inputSchema as z.ZodRawShape);
    assert.ok(sch.safeParse({ type: 'straight', height: 3000, well_handle: '2AF', direction: 90 }).success);
  });
});
