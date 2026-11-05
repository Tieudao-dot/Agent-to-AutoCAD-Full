import { tool } from '@anthropic-ai/claude-agent-sdk';
import { z } from 'zod';
import type { ConfirmRequestMessage } from '../ipc/protocol.js';
import { handles, type AnyTool } from './common.js';

/** Tools answered by the sidecar itself (not forwarded to the plugin). */
export const LOCAL_TOOL_NAMES = ['request_confirmation'];

let seq = 0;

/** Palette-side tools: the confirmation card (feature 11). */
export function interactionTools(show: (req: ConfirmRequestMessage) => void): AnyTool[] {
  return [
    tool(
      'request_confirmation',
      'Show a confirmation card with "Đồng ý" / "Hủy" buttons (and "Xem trước" to highlight the objects) BEFORE any destructive operation that ' +
        'needs confirmation (erase by filter, changing more than 20 objects, deleting layouts/xrefs). text: one short Vietnamese sentence ' +
        'with the count and what will happen, e.g. "Xóa 57 đối tượng trên layer A-TEMP?". handles: the affected objects (for the preview). ' +
        'After calling it, END YOUR TURN without further tool calls; the answer arrives as the next user message.',
      {
        text: z.string().min(1),
        count: z.number().int().min(0).optional(),
        handles: handles.max(5000).optional(),
      },
      async (args) => {
        const id = `confirm-${++seq}`;
        show({ type: 'confirm_request', id, text: args.text, ...(args.count !== undefined ? { count: args.count } : {}), ...(args.handles ? { handles: args.handles } : {}) });
        return {
          content: [{ type: 'text' as const, text: 'The confirmation card is shown to the user. End your turn now; their answer comes in the next message.' }],
        };
      },
      { annotations: { readOnlyHint: true, openWorldHint: false } },
    ) as unknown as AnyTool,
  ];
}
