import { createSdkMcpServer } from '@anthropic-ai/claude-agent-sdk';
import type { AnyTool, ToolBridge } from './common.js';
import { batchTools } from './batch.js';
import { drawTools } from './draw.js';
import { layerTools } from './layers.js';
import { queryTools } from './query.js';
import { systemTools } from './system.js';

export const MCP_SERVER_NAME = 'claudecad';

/** Every ClaudeCAD tool, in the order Claude sees them. */
export function buildTools(bridge: ToolBridge): AnyTool[] {
  return [
    ...queryTools(bridge),
    ...batchTools(bridge),
    ...drawTools(bridge),
    ...layerTools(bridge),
    ...systemTools(bridge),
  ];
}

export function createCadServer(bridge: ToolBridge, version: string) {
  return createSdkMcpServer({
    name: MCP_SERVER_NAME,
    version,
    tools: buildTools(bridge),
  });
}

/** Fully-qualified names as Claude Code sees them: mcp__claudecad__<tool>. */
export function qualifiedToolNames(bridge: ToolBridge): string[] {
  return buildTools(bridge).map((t) => `mcp__${MCP_SERVER_NAME}__${t.name}`);
}
