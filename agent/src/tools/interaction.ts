import { tool } from '@anthropic-ai/claude-agent-sdk';
import { z } from 'zod';
import type { SidecarToPlugin } from '../ipc/protocol.js';
import { handles, type AnyTool } from './common.js';

/** Tools answered by the sidecar itself (not forwarded to the plugin). */
export const LOCAL_TOOL_NAMES = ['request_confirmation', 'propose_plan', 'report_progress', 'suggest_next_steps', 'propose_skill'];

export const SKILL_NAME_RX = /^[a-z0-9][a-z0-9-]{0,63}$/;

let seq = 0;

/**
 * The SKILL.md text of a drafted skill (frontmatter + body). plan: the machine's service plan; on 2D or 3D the skill
 * gets a "plan:" line, so a standard made on a 2D machine is not loaded on a 3D machine (whose tools it would ask for).
 * Full and an older plugin: no plan line (every plan).
 */
export function skillMarkdown(name: string, description: string, body: string, plan?: string): string {
  // quoted: a ": " or " #" in a Vietnamese description must not break the YAML frontmatter
  const desc = JSON.stringify(description.replace(/\r?\n/g, ' ').replace(/"/g, "'").trim());
  const text = body.replace(/\r\n/g, '\n').trim();
  // Claude sometimes repeats the frontmatter in the body: keep only ours
  const withoutFrontmatter = text.startsWith('---') ? text.replace(/^---\n[\s\S]*?\n---\n?/, '').trim() : text;
  const planLine = plan === '2d' || plan === '3d' ? `plan: ${plan}\n` : '';
  return `---\nname: ${name}\ndescription: ${desc}\n${planLine}---\n\n${withoutFrontmatter}\n`;
}

const textResult = (text: string) => ({ content: [{ type: 'text' as const, text }] });

/** Palette-side tools: confirmation card, plan card, next-step buttons, skill draft card. */
export function interactionTools(emit: (msg: SidecarToPlugin) => void, plan?: string): AnyTool[] {
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
        emit({ type: 'confirm_request', id, text: args.text, ...(args.count !== undefined ? { count: args.count } : {}), ...(args.handles ? { handles: args.handles } : {}) });
        return textResult('The confirmation card is shown to the user. End your turn now; their answer comes in the next message.');
      },
      { annotations: { readOnlyHint: true, openWorldHint: false } },
    ) as unknown as AnyTool,

    tool(
      'propose_plan',
      'Show a plan card with "Làm" / "Sửa kế hoạch" / "Bỏ" buttons BEFORE a big multi-step drawing task (a whole floor plan, a full MEP layout, ' +
        'many sheets, roughly more than 50 new objects or several different drawing steps). summary: one Vietnamese sentence; steps: 2-12 short ' +
        'Vietnamese steps with the key sizes, layers and assumptions. Look at the drawing first (get_drawing_info, list_layers) but do not change it. ' +
        'After calling it, END YOUR TURN; "Làm" arrives as the next user message ("Làm theo kế hoạch."), or the user writes what to change. ' +
        'Not for small or clearly specified requests.',
      {
        summary: z.string().min(1).max(300),
        steps: z.array(z.string().min(1).max(300)).min(1).max(15),
      },
      async (args) => {
        const id = `plan-${++seq}`;
        emit({ type: 'plan_request', id, summary: args.summary, steps: args.steps });
        return textResult('The plan card is shown to the user. End your turn now without changing the drawing; the answer comes in the next message.');
      },
      { annotations: { readOnlyHint: true, openWorldHint: false } },
    ) as unknown as AnyTool,

    tool(
      'report_progress',
      'Tell the palette which step of a multi-step task you are starting, so the user sees "Bước 3/7: Vẽ tường" with ✓ / ● / ○. ' +
        'While carrying out an approved plan ("Làm theo kế hoạch."): call it with step = the plan step (1-based) right BEFORE you start that ' +
        'step; the steps are the ones of your propose_plan card, do not repeat them. For a big task without a plan card (plan cards turned off), ' +
        'call it once at the start with steps = 2-12 short Vietnamese steps and step = 1, then with step only. note: optional short detail of ' +
        'the current step, e.g. "46 đoạn LINE". It does not touch the drawing and works in read-only mode. Not for small tasks.',
      {
        step: z.number().int().min(1).max(15),
        steps: z.array(z.string().min(1).max(120)).min(2).max(15).optional(),
        note: z.string().max(120).optional(),
      },
      async (args) => {
        const steps = args.steps?.map((x) => x.trim()).filter(Boolean);
        const step = steps && steps.length > 0 ? Math.min(args.step, steps.length) : args.step;
        emit({ type: 'progress', step, ...(steps && steps.length > 0 ? { steps } : {}), ...(args.note?.trim() ? { note: args.note.trim() } : {}) });
        return textResult('ok');
      },
      { annotations: { readOnlyHint: true, openWorldHint: false } },
    ) as unknown as AnyTool,

    tool(
      'suggest_next_steps',
      'Show 1-3 buttons under your reply with the most useful NEXT requests the user is likely to make (Vietnamese, imperative, under 60 ' +
        'characters, complete enough to send as-is), e.g. "Ghi kích thước các phòng", "Thêm tên phòng và diện tích", "Bố trí đèn trần". ' +
        'Call it once, as the last tool call of a turn that changed or analysed the drawing. Do not mention the buttons in your text.',
      {
        items: z.array(z.string().min(2).max(80)).min(1).max(3),
      },
      async (args) => {
        emit({ type: 'suggestions', items: args.items, source: 'tool' });
        return textResult('ok');
      },
      { annotations: { readOnlyHint: true, openWorldHint: false } },
    ) as unknown as AnyTool,

    tool(
      'propose_skill',
      'Draft a ClaudeCAD skill (SKILL.md) for the user to review and add with one click — only when the user asks to create a skill / ' +
        'save their standard ("tạo skill", "lưu thành chuẩn"). name: lowercase-hyphen slug (e.g. "chuan-cong-ty-x"); description: when to use ' +
        'it with trigger keywords (Vietnamese, 20-1024 characters); body: Markdown instructions — layer table (name, color, linetype, lineweight), ' +
        'text heights per scale, dimension style, symbols, step-by-step procedure — using the exact values from the conversation. ' +
        'Do not draw anything in the same turn.',
      {
        name: z.string().regex(SKILL_NAME_RX, 'lowercase letters, digits and hyphens'),
        description: z.string().min(20).max(1024),
        body: z.string().min(40).max(60_000),
      },
      async (args) => {
        emit({ type: 'skill_draft', name: args.name, description: args.description, content: skillMarkdown(args.name, args.description, args.body, plan) });
        return textResult('The draft is shown to the user with "Xem / sửa" and "Thêm vào plugin" buttons. Tell them briefly what the skill contains and end your turn.');
      },
      { annotations: { readOnlyHint: true, openWorldHint: false } },
    ) as unknown as AnyTool,
  ];
}
