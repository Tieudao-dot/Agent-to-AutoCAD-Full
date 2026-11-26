import { createSdkMcpServer } from '@anthropic-ai/claude-agent-sdk';
import { z } from 'zod';
import type { SidecarToPlugin } from '../ipc/protocol.js';
import { interactionTools } from './interaction.js';
import type { AnyTool, ToolBridge } from './common.js';
import { advancedTools } from './advanced.js';
import { annotationTools } from './annotation.js';
import { utility2dTools } from './utility2d.js';
import { drawingCheckTools } from './drawingCheck.js';
import { traceTools } from './trace.js';
import { exportDrawingTools } from './exportDrawing.js';
import { batchTools } from './batch.js';
import { blockTools } from './blocks.js';
import { captureTools } from './capture.js';
import { drawTools } from './draw.js';
import { drawingSetTools } from './drawingset.js';
import { estimateTools } from './estimate.js';
import { layerTools } from './layers.js';
import { layoutTools } from './layouts.js';
import { MATCH_PROPS_GENERAL, MATCH_PROPS_TEXT_DESC, modifyTools } from './modify.js';
import { queryTools } from './query.js';
import { solidTools } from './solids.js';
import { standards2dTools } from './standards2d.js';
import { styleTools } from './styles.js';
import { systemTools } from './system.js';
import { viewTools } from './view.js';

export const MCP_SERVER_NAME = 'claudecad';

/**
 * Nâng cấp đợt F4: the tools Claude gets with their full description and schema when a chat opens (the ones nearly
 * every task uses). Every other ClaudeCAD tool of the plan is still there, listed by name only (deferred behind the
 * built-in ToolSearch): Claude loads its schema with ToolSearch when it needs it, then calls it as usual. A name of
 * another plan is simply not on the machine's list. The card tools (interaction.ts) are always loaded too.
 */
export const ALWAYS_LOADED_TOOLS: readonly string[] = [
  // reading, layers, view, the batch
  'get_drawing_info', 'list_entities', 'get_entity', 'list_layers', 'create_layer', 'batch_execute', 'capture_view', 'zoom_extents',
  // drawing and changing
  'draw_line', 'draw_polyline', 'draw_rectangle', 'draw_circle', 'move_entities', 'copy_entities', 'erase_entities', 'set_entity_props',
  // 2D
  'add_text', 'add_dim_linear', 'offset_entity',
  // 3D
  'draw_solid', 'extrude', 'solid_subtract', 'get_solid_info', 'set_view_3d', 'build_3d_from_plan',
];

/** Marks a tool as always loaded (Claude Code reads _meta["anthropic/alwaysLoad"]); others stay deferred. */
export function withLoading(t: AnyTool, always: boolean): AnyTool {
  if (!always) return t;
  return { ...t, _meta: { ...(t._meta ?? {}), 'anthropic/alwaysLoad': true } } as AnyTool;
}

/**
 * Every ClaudeCAD tool, in the order Claude sees them. allowed = the tools of this machine's service plan (sent by
 * the plugin): the others are left out to save tokens. The plugin's dispatcher refuses them anyway (NOT_IN_PLAN).
 */
export function buildTools(bridge: ToolBridge, allowed?: readonly string[], plan?: string): AnyTool[] {
  const all = [
    ...queryTools(bridge),
    ...batchTools(bridge),
    ...drawTools(bridge),
    ...layerTools(bridge),
    ...modifyTools(bridge),
    ...annotationTools(bridge),
    ...utility2dTools(bridge),
    ...blockTools(bridge),
    ...styleTools(bridge),
    ...viewTools(bridge),
    ...captureTools(bridge),
    ...solidTools(bridge),
    ...layoutTools(bridge),
    ...advancedTools(bridge),
    ...standards2dTools(bridge),
    ...drawingSetTools(bridge), // nâng cấp đợt G1
    ...estimateTools(bridge), // nâng cấp đợt G3
    ...drawingCheckTools(bridge), // nâng cấp đợt G2
    ...traceTools(bridge), // nâng cấp đợt G5
    ...exportDrawingTools(bridge), // nâng cấp đợt H2
    ...systemTools(bridge),
  ];
  const kept = allowed ? all.filter((t) => new Set(allowed).has(t.name)) : all;
  const always = new Set(ALWAYS_LOADED_TOOLS);
  return (plan ? kept.map((t) => trimForPlan(t, plan)) : kept).map((t) => withLoading(t, always.has(t.name)));
}

/**
 * Parameters a plan does not have, left out of the schemas Claude sees (tokens, and no invitation to try them):
 * a plan without 2D has no block attributes (text, decided 12/11/2026 C3: the plugin refuses them, NOT_IN_PLAN); a
 * plan without 3D has no 3D pictures in capture_view and no array_rect levels. Full and an older plugin (no plan) see every parameter.
 */
// Test & sửa đợt F1: the notes say "the tools loaded here", not "this machine's plan": a Full machine in the 3D work
// mode gets the same trimmed schemas, and there the way out is the mode switch, not a plan (WorkModeLogic).
export const PLAN_HIDDEN_PARAMS: Record<string, { needs: '2d' | '3d'; params: string[]; note: string }> = {
  define_block: { needs: '2d', params: ['attributes'], note: ' The tools loaded here have no block attributes (text): define blocks from entities only.' },
  insert_block: { needs: '2d', params: ['attributes'], note: ' The tools loaded here have no block attributes (text): do not set attribute values.' },
  capture_view: { needs: '3d', params: ['view', 'direction', 'visual_style'], note: '' },
  // nâng cấp đợt A: levels along Z are 3D (the plugin refuses levels > 1 without 3D: PlanLogic.ThreeDParams)
  array_rect: { needs: '3d', params: ['levels', 'level_spacing'], note: '' },
  // nâng cấp đợt D: a table in the drawing is text (2D); on the 3D plan the 3D schedules only return their numbers
  volume_schedule: { needs: '2d', params: ['table_position', 'title', 'text_height'], note: ' No tables in the drawing with the tools loaded here: show the numbers to the user in the chat.' },
  check_interference: { needs: '2d', params: ['table_position', 'title', 'text_height', 'decimals'], note: ' No clash table in the drawing with the tools loaded here: list the pairs in the chat.' },
  // nâng cấp đợt G3: the estimate table in the drawing is text (2D); the 3D plan gets the numbers + Excel
  cost_estimate: { needs: '2d', params: ['table_position', 'title', 'text_height', 'layer'], note: ' No table in the drawing with the tools loaded here: give the numbers in the chat and the Excel file.' },
};

function trimForPlan(t0: AnyTool, plan: string): AnyTool {
  let t = t0;
  // KFG-25: cost_estimate offers only the quantities of the plan's schedules (volumes: 3D; blocks / areas: 2D)
  if (t.name === 'cost_estimate' && (plan === '2d' || plan === '3d')) {
    const q = plan === '3d' ? z.enum(['volume']) : z.enum(['blocks', 'areas']);
    t = { ...t, inputSchema: { ...(t.inputSchema as Record<string, unknown>), quantities: q.describe('Which schedule gives the quantities.') } } as AnyTool;
  }
  // KT2-14 (kiểm tra độc lập nâng cấp): without 2D, match_properties offers only the general properties (the plugin
  // refuses text / dimension there: PlanLogic.TextValues), so Claude is not invited to a call that is sure to fail
  if (t.name === 'match_properties' && plan !== '2d' && plan !== '2d3d') {
    const shape = { ...(t.inputSchema as Record<string, unknown>), properties: z.array(z.enum(MATCH_PROPS_GENERAL)).optional() };
    return { ...t, description: t.description.replace(MATCH_PROPS_TEXT_DESC, '. Default: all.'), inputSchema: shape } as AnyTool;
  }
  const h = PLAN_HIDDEN_PARAMS[t.name];
  const has = h && (plan === '2d3d' || plan === h.needs);
  if (!h || has) return t;
  const shape = { ...(t.inputSchema as Record<string, unknown>) };
  for (const k of h.params) delete shape[k];
  let description = t.description;
  if (t.name === 'capture_view') description = description.replace(/ 3D: [\s\S]*$/, '');
  else description = description.replace(/ Optional attribute definitions: [\s\S]*?\]\. /, ' ').replace(/ and attribute values \{TAG: value\}/, '')
    .replace(/ Read-only unless table_position \([^)]*\)\./, ' Read-only.') + h.note;
  return { ...t, description, inputSchema: shape } as AnyTool;
}

/** emit: palette messages of the local tools (confirmation / plan / suggestion / skill draft cards). */
/** skillPlan: the machine's own plan for drafted skills (a Full machine in the 2D work mode still drafts Full skills). */
/** allLoaded (Test & sửa đợt F4, the switch-off): every tool in full, nothing deferred, as before F4. */
export function createCadServer(bridge: ToolBridge, version: string, emit?: (msg: SidecarToPlugin) => void, allowed?: readonly string[], plan?: string, skillPlan?: string, allLoaded = false) {
  return createSdkMcpServer({
    name: MCP_SERVER_NAME,
    version,
    ...(allLoaded ? { alwaysLoad: true } : {}),
    tools: [...buildTools(bridge, allowed, plan), ...(emit ? interactionTools(emit, skillPlan ?? plan).map((t) => withLoading(t, true)) : [])],
  });
}
