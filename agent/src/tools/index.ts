import { createSdkMcpServer } from '@anthropic-ai/claude-agent-sdk';
import type { AnyTool, ToolBridge } from './common.js';
import { systemTools } from './system.js';

export const MCP_SERVER_NAME = 'claudecad';

/** Every ClaudeCAD tool, in the order Claude sees them. */
export function buildTools(bridge: ToolBridge): AnyTool[] {
  return [...systemTools(bridge)];
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
