/**
 * Nâng cấp đợt H2 (docs/ban-giao/nang-cap-h2.md): export_drawing — schema, loading, the card of an existing file, plans.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { F5_CARD_TOOLS } from '../src/agent/confirmation.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { ALWAYS_LOADED_TOOLS, buildTools } from '../src/tools/index.js';
import { EXPORT_FORMATS, EXPORT_VERSIONS } from '../src/tools/exportDrawing.js';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const plugin = (...p: string[]) => fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', ...p), 'utf8');
const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };

describe('H2: export_drawing', () => {
  const tools = new Map(buildTools(bridge).map((t) => [t.name, t as any]));
  const ex = tools.get('export_drawing');
  const parse = (v: Record<string, unknown>) => Object.entries(v).every(([k, x]) => k in ex.inputSchema && ex.inputSchema[k].safeParse(x).success);

  it('one tool for the three formats, deferred, a read', () => {
    assert.ok(ex);
    assert.deepEqual([...EXPORT_FORMATS], ['dxf', 'dwg', 'fbx']);
    assert.deepEqual([...EXPORT_VERSIONS], ['2018', '2013', '2010', '2007', '2004', '2000']);
    assert.ok(!ALWAYS_LOADED_TOOLS.includes('export_drawing'));
    assert.ok(!ex._meta?.['anthropic/alwaysLoad']);
    assert.equal(ex.annotations?.readOnlyHint, true);
    assert.match(ex.description, /open drawing keeps its file, name and version/);
    assert.match(ex.description, /SketchUp/);
  });

  it('schema', () => {
    assert.ok(parse({ format: 'dwg', version: '2007', output: 'gui-doi-tac.dwg' }));
    assert.ok(parse({ format: 'dxf', layers: ['A-WALL'] }));
    assert.ok(parse({ format: 'fbx', handles: ['2F'] }));
    assert.ok(!parse({ format: 'skp' }));
    assert.ok(!parse({ format: 'dwg', version: '1998' }));
  });

  it('an existing file needs the card; the 3D value is on the plan list of the plugin', () => {
    assert.ok(F5_CARD_TOOLS.has('export_drawing'));
    const plans = plugin('Core', 'Plans.cs');
    assert.match(plans, /Add\(ToolGroup\.Common, "export_drawing"\)/);
    assert.match(plans, /\["export_drawing"\] = \("format", new\[\] \{ "fbx" \}\)/);
    assert.match(plugin('UI', 'ToolLabels.cs'), /\["export_drawing"\] = "Xuất DXF \/ DWG \/ FBX"/);
  });

  it('Test & sửa H2: the 2D plan is not offered fbx; 3D and Full are', () => {
    const fmt = (plan?: '2d' | '3d' | '2d3d') => ((buildTools(bridge, ['export_drawing'], plan)[0]!.inputSchema as any).format.options as string[]).slice().sort();
    assert.deepEqual(fmt('2d'), ['dwg', 'dxf']);
    assert.deepEqual(fmt('3d'), ['dwg', 'dxf', 'fbx']);
    assert.deepEqual(fmt('2d3d'), ['dwg', 'dxf', 'fbx']);
  });

  it('one line in the prompt of every plan', () => {
    for (const p of ['2d', '3d', '2d3d'] as const) assert.match(systemPromptFor(p), /export_drawing/);
  });
});
