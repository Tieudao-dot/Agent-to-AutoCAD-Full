import type { SDKUserMessage } from '@anthropic-ai/claude-agent-sdk';
import type { UserMessage } from '../ipc/protocol.js';

/** Skill names the palette's "Chuẩn" selector can choose. */
export const STANDARD_LABELS: Record<string, string> = {
  've-kien-truc': 'kiến trúc',
  've-mep': 'điện nước (MEP)',
  've-ha-tang': 'hạ tầng / quy hoạch',
};

export const MAX_LISTED_HANDLES = 500;

/**
 * Builds the text that precedes the user's own words: active drawing, chosen
 * standard and print scale, the current selection, and whether the previous
 * request was edited. Kept as bracketed lines so Claude can tell context from
 * the request itself.
 */
export function contextPreamble(msg: UserMessage): string[] {
  const lines: string[] = [];
  if (msg.drawing) lines.push(`[Bản vẽ đang mở: ${msg.drawing}]`);
  const std = msg.context?.standard;
  if (std) {
    const label = STANDARD_LABELS[std] ?? std;
    lines.push(`[Chuẩn bản vẽ người dùng chọn: ${label} — load and follow the skill "${std}" for this request]`);
  }
  if (msg.context?.scale) lines.push(`[Tỷ lệ in người dùng chọn: ${msg.context.scale} — size text/dimensions for this scale]`);
  if (msg.context?.read_only) {
    lines.push('[CHỈ XEM: người dùng bật "Chỉ xem" cho bản vẽ này — only read, measure, look (capture_view) and answer; do not call tools that change the drawing]');
  }
  if (msg.context?.notes) lines.push(`[Ghi chú dự án của người dùng — always apply, they override generic defaults:\n${msg.context.notes}\n]`);
  if (msg.context?.visual_check === false) lines.push('[Người dùng tắt tự kiểm tra bằng ảnh: call capture_view only when asked]');
  if (msg.context?.plan_first === false) lines.push('[Người dùng tắt thẻ kế hoạch: do not call propose_plan, just do the task]');
  if (msg.context?.suggestions === false) lines.push('[Người dùng tắt nút gợi ý: do not call suggest_next_steps]');
  const sel = msg.attachments?.selection;
  if (sel && sel.handles.length > 0) {
    const n = sel.count ?? sel.handles.length;
    const listed = sel.handles.slice(0, MAX_LISTED_HANDLES).join(', ');
    const more = sel.handles.length > MAX_LISTED_HANDLES ? ` … (+${sel.handles.length - MAX_LISTED_HANDLES})` : '';
    lines.push(
      `[Đối tượng người dùng đang chọn: ${n}${sel.summary ? ` (${sel.summary})` : ''}. ` +
        `"mấy cái này / các đối tượng này / chúng" refers to them. Handles: ${listed}${more}]`,
    );
  }
  if (msg.edited) {
    lines.push('[Người dùng đã sửa lại yêu cầu ở lượt trước; mọi thay đổi bản vẽ của lượt trước đã được hoàn tác (U). Làm lại theo yêu cầu mới dưới đây.]');
  }
  if (msg.confirm === 'yes') lines.push('[Người dùng bấm nút Đồng ý trên thẻ xác nhận]');
  if (msg.confirm === 'no') lines.push('[Người dùng bấm nút Hủy trên thẻ xác nhận: không thực hiện thao tác đó]');
  if (msg.plan === 'approved') lines.push('[Người dùng bấm "Làm" trên thẻ kế hoạch: execute the plan you proposed exactly, without asking again]');
  return lines;
}

/** Converts a palette message into the SDK user message (text, or images + text blocks). */
export function toSdkUserMessage(msg: UserMessage): SDKUserMessage {
  const text = [...contextPreamble(msg), msg.text].join('\n');
  const images = msg.attachments?.images ?? [];
  if (images.length === 0) {
    return { type: 'user', message: { role: 'user', content: text }, parent_tool_use_id: null };
  }
  return {
    type: 'user',
    message: {
      role: 'user',
      content: [
        ...images.map((img) => ({
          type: 'image' as const,
          source: { type: 'base64' as const, media_type: img.media_type, data: img.data },
        })),
        {
          type: 'text' as const,
          text:
            `${text}\n[Người dùng đính kèm ${images.length} ảnh ở trên (phác thảo, ảnh scan hoặc ảnh chụp màn hình). ` +
            'Read dimensions and layout from it; ask if a needed size cannot be read.]',
        },
      ],
    },
    parent_tool_use_id: null,
  };
}

/** Short Vietnamese summary of a tool result for the palette's activity log. */
export function summarizeResult(tool: string, result: unknown): { summary?: string; handles?: string[] } {
  if (typeof result !== 'object' || result === null) return {};
  const r = result as Record<string, unknown>;
  const handles = Array.isArray(r.handles) ? (r.handles.filter((h) => typeof h === 'string') as string[]) : undefined;
  const parts: string[] = [];
  if (tool === 'batch_execute' && typeof r.ok_count === 'number') {
    parts.push(`${r.ok_count} lệnh OK`);
    if (typeof r.error_count === 'number' && r.error_count > 0) parts.push(`${r.error_count} lỗi`);
  }
  if (typeof r.erased === 'number') parts.push(`xóa ${r.erased}`);
  else if (handles && handles.length > 0) parts.push(`${handles.length} đối tượng`);
  else if (typeof r.total === 'number') parts.push(`${r.total} kết quả`);
  else if (typeof r.count === 'number') parts.push(`${r.count}`);
  if (typeof r.area_m2 === 'number') parts.push(`${r.area_m2} m²`);
  if (typeof r.width === 'number' && typeof r.height === 'number' && tool === 'capture_view') parts.push(`ảnh ${r.width}×${r.height}`);
  if (typeof r.distance === 'number') parts.push(`${r.distance} mm`);
  return { ...(parts.length ? { summary: parts.join(', ') } : {}), ...(handles?.length ? { handles } : {}) };
}
