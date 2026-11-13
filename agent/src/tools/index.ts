import { createSdkMcpServer } from '@anthropic-ai/claude-agent-sdk';
import type { SidecarToPlugin } from '../ipc/protocol.js';
import { interactionTools } from './interaction.js';
import type { AnyTool, ToolBridge } from './common.js';
import { advancedTools } from './advanced.js';
import { annotationTools } from './annotation.js';
import { utility2dTools } from './utility2d.js';
import { batchTools } from './batch.js';
import { blockTools } from './blocks.js';
import { captureTools } from './capture.js';
import { drawTools } from './draw.js';
import { layerTools } from './layers.js';
import { layoutTools } from './layouts.js';
import { modifyTools } from './modify.js';
import { queryTools } from './query.js';
import { solidTools } from './solids.js';
import { styleTools } from './styles.js';
import { systemTools } from './system.js';
import { viewTools } from './view.js';

export const MCP_SERVER_NAME = 'claudecad';

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
    ...systemTools(bridge),
  ];
  const kept = allowed ? all.filter((t) => new Set(allowed).has(t.name)) : all;
  return plan ? kept.map((t) => trimForPlan(t, plan)) : kept;
}

/**
 * Parameters a plan does not have, left out of the schemas Claude sees (tokens, and no invitation to try them):
 * a plan without 2D has no block attributes (text, decided 12/11/2026 C3: the plugin refuses them, NOT_IN_PLAN); a
 * plan without 3D has no 3D pictures in capture_view and no array_rect levels. Full and an older plugin (no plan) see every parameter.
 */
export const PLAN_HIDDEN_PARAMS: Record<string, { needs: '2d' | '3d'; params: string[]; note: string }> = {
  define_block: { needs: '2d', params: ['attributes'], note: ' This machine\'s plan has no block attributes (text): define blocks from entities only.' },
  insert_block: { needs: '2d', params: ['attributes'], note: ' This machine\'s plan has no block attributes (text): do not set attribute values.' },
  capture_view: { needs: '3d', params: ['view', 'direction', 'visual_style'], note: '' },
  // nâng cấp đợt A: levels along Z are 3D (the plugin refuses levels > 1 without 3D: PlanLogic.ThreeDParams)
  array_rect: { needs: '3d', params: ['levels', 'level_spacing'], note: '' },
};

function trimForPlan(t: AnyTool, plan: string): AnyTool {
  const h = PLAN_HIDDEN_PARAMS[t.name];
  const has = h && (plan === '2d3d' || plan === h.needs);
  if (!h || has) return t;
  const shape = { ...(t.inputSchema as Record<string, unknown>) };
  for (const k of h.params) delete shape[k];
  let description = t.description;
  if (t.name === 'capture_view') description = description.replace(/ 3D: [\s\S]*$/, '');
  else description = description.replace(/ Optional attribute definitions: [\s\S]*?\]\. /, ' ').replace(/ and attribute values \{TAG: value\}/, '') + h.note;
  return { ...t, description, inputSchema: shape } as AnyTool;
}

/** emit: palette messages of the local tools (confirmation / plan / suggestion / skill draft cards). */
export function createCadServer(bridge: ToolBridge, version: string, emit?: (msg: SidecarToPlugin) => void, allowed?: readonly string[], plan?: string) {
  return createSdkMcpServer({
    name: MCP_SERVER_NAME,
    version,
    tools: [...buildTools(bridge, allowed, plan), ...(emit ? interactionTools(emit, plan) : [])],
  });
}
