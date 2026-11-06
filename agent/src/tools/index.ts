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

/** Every ClaudeCAD tool, in the order Claude sees them. */
export function buildTools(bridge: ToolBridge): AnyTool[] {
  return [
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
}

/** emit: palette messages of the local tools (confirmation / plan / suggestion / skill draft cards). */
export function createCadServer(bridge: ToolBridge, version: string, emit?: (msg: SidecarToPlugin) => void) {
  return createSdkMcpServer({
    name: MCP_SERVER_NAME,
    version,
    tools: [...buildTools(bridge), ...(emit ? interactionTools(emit) : [])],
  });
}

/** Fully-qualified names as Claude Code sees them: mcp__claudecad__<tool>. */
export function qualifiedToolNames(bridge: ToolBridge): string[] {
  return buildTools(bridge).map((t) => `mcp__${MCP_SERVER_NAME}__${t.name}`);
}
