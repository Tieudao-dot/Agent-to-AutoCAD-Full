/**
 * Hard safety net for destructive operations (spec §9 rule 6, §10).
 *
 * The primary mechanism is the system prompt: Claude must report the count and ask
 * before erasing by filter or modifying more than 20 objects. This gate enforces
 * the "> 20 objects" part mechanically: such a call is refused unless the user's
 * message of the current turn is a confirmation ("đồng ý", "ok", "có"...).
 */

export const CONFIRM_THRESHOLD = 20;

/** Tools that change or remove existing entities, and the param holding their handles. */
const DESTRUCTIVE: Record<string, (p: Record<string, unknown>) => number> = {
  erase_entities: (p) => len(p.handles),
  move_entities: (p) => len(p.handles),
  rotate_entities: (p) => len(p.handles),
  scale_entities: (p) => len(p.handles),
  mirror_entities: (p) => (p.keep_original === false ? len(p.handles) : 0),
  explode_entities: (p) => len(p.handles),
  set_entity_props: (p) => len(p.handles),
  join_entities: (p) => len(p.handles),
  edit_text: () => 1,
  edit_polyline: () => 1,
  set_block_attributes: () => 1,
  set_dynamic_props: () => 1,
  trim_entities: (p) => len(p.handles),
  extend_entities: (p) => len(p.handles),
};

function len(v: unknown): number {
  return Array.isArray(v) ? v.length : 0;
}

/** Number of existing entities a call would modify/erase (batch_execute summed over its calls). */
export function affectedCount(tool: string, params: Record<string, unknown>): number {
  if (tool === 'batch_execute' && Array.isArray(params.calls)) {
    let n = 0;
    for (const c of params.calls as Array<{ tool?: unknown; params?: unknown }>) {
      if (c && typeof c.tool === 'string') n += affectedCount(c.tool, (c.params as Record<string, unknown>) ?? {});
    }
    return n;
  }
  const f = DESTRUCTIVE[tool];
  return f ? f(params) : 0;
}

const PHRASES = [
  'đồng ý', 'dong y', 'xác nhận', 'xac nhan', 'làm đi', 'lam di', 'cứ làm', 'cu lam', 'tiếp tục', 'tiep tuc',
  'chắc chắn', 'chac chan', 'xóa đi', 'xoa di', 'xóa luôn', 'xoa luon', 'được rồi', 'duoc roi', 'go ahead', 'đổi đi', 'sửa đi',
];
/** Accepted anywhere in a short reply. */
const ANYWHERE = new Set(['ok', 'okay', 'oke', 'okie', 'yes', 'yep', 'confirm']);
/** Accepted only as the first word ("có, xóa đi" yes; "có bao nhiêu..." / "có thể xóa..." no). */
const LEADING = new Set(['có', 'ừ', 'ừm', 'uh', 'y', 'đúng', 'được', 'duoc', 'chuẩn']);

/** True if the user's message reads as an explicit confirmation. */
export function isConfirmation(text: string | undefined): boolean {
  if (!text) return false;
  const t = text.toLowerCase().normalize('NFC');
  if (/\b(không|khong|đừng|dung lai|đừng xóa|no)\b/u.test(t) && !/không sao|khong sao/u.test(t)) {
    // "không" alone is a refusal; "không sao" (no problem) is fine
    if (/^\s*(không|khong|no|đừng)/u.test(t)) return false;
  }
  if (PHRASES.some((ph) => t.includes(ph))) return true;
  const tokens = t.split(/[\s,.!?;:()"'…-]+/u).filter(Boolean);
  if (tokens.length === 0 || tokens.length > 12) return false; // confirmations are short replies
  if (tokens.some((w) => ANYWHERE.has(w))) return true;
  const first = tokens[0]!;
  if (!LEADING.has(first)) return false;
  // "có thể ...?" / "có bao nhiêu ...?" are questions, not answers
  return !(first === 'có' && /^(thể|the|bao|phải|nên|cần)$/u.test(tokens[1] ?? ''));
}

/**
 * Returns an error message if the call must be confirmed first, otherwise undefined.
 */
export function confirmationError(tool: string, params: Record<string, unknown>, userText: string | undefined): string | undefined {
  const n = affectedCount(tool, params);
  if (n <= CONFIRM_THRESHOLD) return undefined;
  if (isConfirmation(userText)) return undefined;
  return (
    `Confirmation required: this ${tool} call would modify or erase ${n} existing objects (more than ${CONFIRM_THRESHOLD}). ` +
    'Do not retry now. Tell the user in Vietnamese how many objects and what will change, ask "Cậu có đồng ý không?", ' +
    'and end your turn. Run the operation only after the user confirms in their next message.'
  );
}
