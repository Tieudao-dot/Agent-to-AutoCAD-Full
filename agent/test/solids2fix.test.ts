/**
 * Fixes of the 3D phase 2 test round (docs/ban-giao/ve-3d-test-2.md), assistant side.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { z } from 'zod';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';

const calls: Array<{ tool: string; params: Record<string, unknown> }> = [];
const bridge: ToolBridge = {
  call: async (tool, params) => {
    calls.push({ tool, params });
    return { id: 'x', ok: true, result: { handles: ['1A'] } };
  },
  status: () => {},
};
const tools = buildTools(bridge);
const byName = (n: string) => tools.find((t) => t.name === n)!;
const ok = (n: string, p: unknown) => z.object(byName(n).inputSchema as z.ZodRawShape).safeParse(p).success;

describe('3D phase 2 test round fixes', () => {
  it('a closed shell (faces: []) needs no topology_id, and the description says so', () => {
    assert.ok(ok('shell_solid', { handle: '2F4A', thickness: 20, faces: [] }));
    assert.match(byName('shell_solid').description, /faces: \[\]\) = a closed hollow shell, no topology_id needed/);
  });

  it('check_interference takes overlapping lists and says each pair is checked once', () => {
    assert.ok(ok('check_interference', { handles: ['A', 'B'], other_handles: ['B', 'A', 'C'] }));
    assert.match(byName('check_interference').description, /each pair is checked once/);
  });
});
