import { createSdkMcpServer } from '@anthropic-ai/claude-agent-sdk';
import type { SidecarToPlugin } from '../ipc/protocol.js';
import { interactionTools } from './interaction.js';
import type { AnyTool, ToolBridge } from './common.js';
import { advancedTools } from './advanced.js';
import { annotationTools } from './annotation.js';
import { batchTools } from './batch.js';
import { blockTools } from './blocks.js';
import { captureTools } from './capture.js';
import { drawTools } from './draw.js';
import { layerTools } from './layers.js';
import { layoutTools } from './layouts.js';
import { modifyTools } from './modify.js';
import { queryTools } from './query.js';
import { styleTools } from './styles.js';
import { systemTools } from './system.js';
import { viewTools } from './view.js';

export const MCP_SERVER_NAME = 'claudecad';

/**
 * Every ClaudeCAD tool, in the order Claude sees them. allowed = the tools of this machine's service plan (sent by
 * the plugin): the others are left out to save tokens. The plugin's dispatcher refuses them anyway (NOT_IN_PLAN).
 */
export function buildTools(bridge: ToolBridge, allowed?: readonly string[]): AnyTool[] {
  const all = [
    ...queryTools(bridge),
    ...batchTools(bridge),
    ...drawTools(bridge),
    ...layerTools(bridge),
    ...modifyTools(bridge),
    ...annotationTools(bridge),
    ...blockTools(bridge),
    ...styleTools(bridge),
    ...viewTools(bridge),
    ...captureTools(bridge),
    ...layoutTools(bridge),
    ...advancedTools(bridge),
    ...systemTools(bridge),
  ];
  if (!allowed) return all;
  const keep = new Set(allowed);
  return all.filter((t) => keep.has(t.name));
}

/** emit: palette messages of the local tools (confirmation / plan / suggestion / skill draft cards). */
export function createCadServer(bridge: ToolBridge, version: string, emit?: (msg: SidecarToPlugin) => void, allowed?: readonly string[]) {
  return createSdkMcpServer({
    name: MCP_SERVER_NAME,
    version,
    tools: [...buildTools(bridge, allowed), ...(emit ? interactionTools(emit) : [])],
  });
}
