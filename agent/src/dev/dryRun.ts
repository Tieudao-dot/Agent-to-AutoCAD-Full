/**
 * Dry run: drives the real Claude agent against an in-memory fake drawing
 * instead of AutoCAD, and prints every tool call. Lets you check agent
 * behaviour (skill use, batching, confirmations) on any machine.
 *
 *   npm run build && node dist/dev/dryRun.js "Vẽ mặt bằng phòng 4x6m, tường 220..." ["đồng ý" ...]
 *
 * Each extra argument is sent as a follow-up user turn. Needs Claude credentials
 * (claude login or ANTHROPIC_API_KEY). Costs real tokens.
 */
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { confirmationError } from '../agent/confirmation.js';
import { AgentSession } from '../agent/session.js';
import type { SidecarToPlugin, ToolResponse } from '../ipc/protocol.js';

interface FakeEntity {
  handle: string;
  type: string;
  layer: string;
  tool: string;
  params: Record<string, unknown>;
}

/** Minimal stateful stand-in for the plugin: enough for the agent to plan and draw. */
export class FakeDrawing {
  private next = 0x100;
  layers = new Map<string, Record<string, unknown>>([['0', { name: '0', color: 7, linetype: 'Continuous', current: true }]]);
  textStyles = new Set(['Standard']);
  dimStyles = new Set(['ISO-25']);
  blocks = new Set<string>();
  entities: FakeEntity[] = [];
  currentLayer = '0';

  constructor(seed: FakeEntity[] = []) {
    this.entities.push(...seed);
  }

  private newHandle(): string {
    return (this.next++).toString(16).toUpperCase();
  }

  private requireLayer(p: Record<string, unknown>): void {
    const l = p.layer as string | undefined;
    if (l && !this.layers.has(l)) {
      throw { code: 'LAYER_NOT_FOUND', message: `Layer '${l}' does not exist. Call create_layer first, or list_layers to see existing layers.` };
    }
  }

  private created(tool: string, p: Record<string, unknown>, type: string): Record<string, unknown> {
    this.requireLayer(p);
    const handle = this.newHandle();
    this.entities.push({ handle, type, layer: (p.layer as string) ?? this.currentLayer, tool, params: p });
    return { handles: [handle] };
  }

  exec(tool: string, p: Record<string, unknown>): Record<string, unknown> {
    if (tool.startsWith('draw_') || tool.startsWith('add_') || tool === 'insert_block') {
      if (tool === 'insert_block' && !this.blocks.has(p.name as string)) throw { code: 'BLOCK_NOT_FOUND', message: `Block '${p.name}' is not defined.` };
      return this.created(tool, p, tool.replace(/^(draw|add)_/, '').toUpperCase());
    }
    switch (tool) {
      case 'ping':
        return { pong: true, acad_version: '24.1 (fake)', drawing: 'dry-run.dwg' };
      case 'capture_view':
        // 1×1 grey PNG: the dry run has no graphics
        return {
          image_png_base64: 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mN8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==',
          width: 1, height: 1, region: 'view', min: [0, 0, 0], max: [1000, 1000, 0], mm_per_pixel: 1000,
          note: 'dry run: no real picture',
        };
      case 'get_drawing_info':
        return {
          drawing: 'dry-run.dwg', units: 'millimeters', insunits: 4, entity_count: this.entities.length,
          by_layer: Object.fromEntries([...new Set(this.entities.map((e) => e.layer))].map((l) => [l, this.entities.filter((e) => e.layer === l).length])),
          current_layer: this.currentLayer, current_text_style: 'Standard', current_dim_style: 'ISO-25', layer_count: this.layers.size,
        };
      case 'list_layers':
        return { count: this.layers.size, current: this.currentLayer, layers: [...this.layers.values()] };
      case 'create_layer': {
        const existed = this.layers.has(p.name as string);
        this.layers.set(p.name as string, { ...p, current: false });
        return { name: p.name, created: !existed, handles: [this.newHandle()] };
      }
      case 'set_current_layer':
        this.requireLayer({ layer: p.name });
        this.currentLayer = p.name as string;
        return { current: p.name };
      case 'list_styles':
        return { text_styles: [...this.textStyles].map((n) => ({ name: n })), dim_styles: [...this.dimStyles].map((n) => ({ name: n })), mleader_styles: ['Standard'] };
      case 'create_text_style':
        this.textStyles.add(p.name as string);
        return { name: p.name, created: true, handles: [this.newHandle()] };
      case 'create_dim_style':
        this.dimStyles.add(p.name as string);
        return { name: p.name, created: true, handles: [this.newHandle()] };
      case 'set_current_styles':
        return { ...p };
      case 'list_block_defs':
        return { count: this.blocks.size, blocks: [...this.blocks].map((name) => ({ name })) };
      case 'define_block':
        this.blocks.add(p.name as string);
        return { name: p.name, handles: [this.newHandle()] };
      case 'list_entities':
      case 'count_entities': {
        const layers = p.layers as string[] | undefined;
        const hits = this.entities.filter((e) => !layers || layers.includes(e.layer));
        return tool === 'count_entities'
          ? { total: hits.length }
          : { total: hits.length, returned: hits.length, entities: hits.slice(0, 200).map((e) => ({ handle: e.handle, type: e.type, layer: e.layer })) };
      }
      case 'erase_entities': {
        const hs = new Set(p.handles as string[]);
        const before = this.entities.length;
        this.entities = this.entities.filter((e) => !hs.has(e.handle));
        return { erased: before - this.entities.length, handles: [...hs] };
      }
      case 'batch_execute': {
        const results = (p.calls as Array<{ tool: string; params?: Record<string, unknown> }>).map((c, index) => {
          try {
            return { index, tool: c.tool, ok: true, result: this.exec(c.tool, c.params ?? {}) };
          } catch (e) {
            return { index, tool: c.tool, ok: false, error: e };
          }
        });
        const handles = results.flatMap((r) => ((r.ok && (r.result as any)?.handles) || []) as string[]);
        return { ok_count: results.filter((r) => r.ok).length, error_count: results.filter((r) => !r.ok).length, handles, results };
      }
      default:
        return { ok: true, handles: (p.handles as string[]) ?? [], note: `${tool} simulated` };
    }
  }

  call(tool: string, p: Record<string, unknown>): ToolResponse {
    try {
      return { id: 'dry', ok: true, result: this.exec(tool, p) };
    } catch (e) {
      const err = e as { code?: string; message?: string };
      return { id: 'dry', ok: false, error: { code: err.code ?? 'ACAD_ERROR', message: err.message ?? String(e) } };
    }
  }
}

async function main(): Promise<void> {
  const prompts = process.argv.slice(2);
  if (prompts.length === 0) {
    console.error('usage: node dist/dev/dryRun.js "<message>" ["<follow-up>" ...]');
    process.exit(2);
  }
  const here = path.dirname(fileURLToPath(import.meta.url));
  const skillsDir = process.env.CLAUDECAD_SKILLS_DIR ?? path.resolve(here, '..', '..', '..', 'skills');
  const drawing = new FakeDrawing();
  let session!: AgentSession;
  let endTurn: () => void = () => {};
  const log: string[] = [];

  session = new AgentSession({
    emit: (m: SidecarToPlugin) => {
      if (m.type === 'agent_text') process.stdout.write(m.delta);
      else if (m.type === 'agent_text_end') process.stdout.write('\n');
      else if (m.type === 'agent_error') console.log(`\n[agent_error] ${m.message}`);
      else if (m.type === 'turn_end') {
        console.log(`\n[turn_end]${m.error ? ' error: ' + m.error : ''}${m.cancelled ? ' cancelled' : ''}`);
        endTurn();
      }
    },
    bridge: {
      call: async (tool, params) => {
        const block = confirmationError(tool, params, session.lastUserText);
        const resp: ToolResponse = block ? { id: 'dry', ok: false, error: { code: 'INVALID_PARAMS', message: block } } : drawing.call(tool, params);
        const summary = tool === 'batch_execute'
          ? `batch_execute(${(params.calls as unknown[]).length} calls: ${[...new Set((params.calls as Array<{ tool: string }>).map((c) => c.tool))].join(', ')})`
          : `${tool}(${JSON.stringify(params).slice(0, 160)})`;
        const line = `  → ${summary}${resp.ok ? '' : `  ✗ ${resp.error.code}: ${resp.error.message.slice(0, 120)}`}`;
        log.push(line);
        console.log(line);
        return resp;
      },
      status: () => {},
    },
    version: 'dry-run',
    workDir: fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-dry-')),
    skillsDir,
    model: process.env.CLAUDECAD_MODEL || 'claude-opus-5-5',
    fallbackModel: process.env.CLAUDECAD_FALLBACK_MODEL || 'claude-sonnet-5-5',
    log: (m) => {
      if (process.env.CLAUDECAD_DRY_VERBOSE) console.error(m);
    },
  });

  for (const text of prompts) {
    console.log(`\n=== user: ${text}`);
    const done = new Promise<void>((r) => (endTurn = r));
    session.send(text, 'dry-run.dwg');
    await done;
  }
  console.log('\n=== layers used by entities:', [...new Set(drawing.entities.map((e) => e.layer))].join(', ') || '(none)');
  console.log('=== entities:', drawing.entities.length, ' text heights:',
    [...new Set(drawing.entities.filter((e) => e.tool === 'add_text').map((e) => e.params.height))].join(', ') || '-');
  session.close();
  process.exit(0);
}

const isMain = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) void main();
