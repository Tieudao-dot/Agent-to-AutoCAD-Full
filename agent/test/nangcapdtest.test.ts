/**
 * Test & sửa đợt D (13/11/2026, docs/ban-giao/nang-cap-d-test.md): PRESSPULL as Claude sees it after the fixes (ND-2:
 * add / subtract need a target; mode "new" does not change the target, so no card is asked for it).
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { confirmationError, existingSolidsChanged, type GateState } from '../src/agent/confirmation.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const none = {} as GateState;

describe('Test & sửa đợt D: presspull', () => {
  it('the description says add / subtract need a target', () => {
    const t = buildTools(bridge).find((x) => x.name === 'presspull');
    assert.ok(t, 'presspull is registered');
    assert.match(t!.description ?? '', /add \/ subtract need target/);
  });

  it('mode new with a target asks no card; add / subtract with a target do', () => {
    const prof = { profile_points: [[0, 0], [100, 0], [100, 100]], distance: 100, target: 'B2' };
    assert.deepEqual(existingSolidsChanged('presspull', { ...prof, mode: 'new' }, none), []);
    assert.equal(confirmationError('presspull', { ...prof, mode: 'new' }, none), undefined);
    for (const mode of ['add', 'subtract', 'auto']) {
      assert.deepEqual(existingSolidsChanged('presspull', { ...prof, mode }, none), ['B2']);
      assert.match(confirmationError('presspull', { ...prof, mode }, none) ?? '', /Confirmation required/);
    }
  });
});
