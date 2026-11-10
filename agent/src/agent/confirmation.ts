/**
 * Hard safety net for destructive operations (spec §9 rule 6, §10).
 *
 * The primary mechanism is the system prompt: Claude must report the count and ask
 * before erasing by filter or modifying more than 20 objects. This gate enforces
 * the "> 20 objects" part mechanically: once a turn has changed or removed more than
 * 20 existing objects in total (summed over all its calls, so splitting into chunks of
 * 20 does not slip through), further destructive calls are refused unless the user's
 * message answers a question of the previous turn with a short confirmation
 * ("đồng ý", "ok", "có, xóa đi"...), or came from the "Đồng ý" button of a card.
 * "vẽ lại rồi xóa đi các đường cũ" as a first request is NOT a confirmation.
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
  reset_dynamic_block: (p) => len(p.handles),
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

/** True if the user's message reads as an explicit confirmation (a short reply, the yes-word near its start). */
export function isConfirmation(text: string | undefined): boolean {
  if (!text) return false;
  const t = text.toLowerCase().normalize('NFC');
  if (/\b(không|khong|đừng|dung lai|đừng xóa|no)\b/u.test(t) && !/không sao|khong sao/u.test(t)) {
    // "không" alone is a refusal; "không sao" (no problem) is fine
    if (/^\s*(không|khong|no|đừng)/u.test(t)) return false;
  }
  const tokens = t.split(/[\s,.!?;:()"'…-]+/u).filter(Boolean);
  if (tokens.length === 0 || tokens.length > 12) return false; // confirmations are short replies
  // "đồng ý", "ừ xóa đi", "thôi được rồi làm đi": the phrase opens the reply. A request that merely
  // contains "xóa đi" further on ("vẽ lại rồi xóa đi các đường cũ") is not an answer.
  const head = tokens.slice(0, 3).join(' ');
  if (PHRASES.some((ph) => head.includes(ph))) return true;
  if (tokens.some((w) => ANYWHERE.has(w))) return true;
  const first = tokens[0]!;
  if (!LEADING.has(first)) return false;
  // "có thể ...?" / "có bao nhiêu ...?" are questions, not answers
  return !(first === 'có' && /^(thể|the|bao|phải|nên|cần)$/u.test(tokens[1] ?? ''));
}

export interface GateState {
  /** The user's message of the current turn. */
  userText: string | undefined;
  /** The previous turn asked the user something (a "?" in Claude's reply, or this gate refused a call). */
  answersQuestion: boolean;
  /** The turn was started by the "Đồng ý" button of a confirmation / plan card. */
  confirmedByButton?: boolean;
  /** Existing objects already changed or removed by earlier calls of this turn. */
  alreadyAffected: number;
}

/** True when the user's message counts as a "yes" to a question Claude asked. */
export function userConfirmed(g: GateState): boolean {
  return !!g.confirmedByButton || (g.answersQuestion && isConfirmation(g.userText));
}

/**
 * Returns an error message if the call must be confirmed first, otherwise undefined.
 * The count is cumulative over the turn (g.alreadyAffected + this call).
 */
export function confirmationError(tool: string, params: Record<string, unknown>, g: GateState): string | undefined {
  const n = affectedCount(tool, params);
  if (n === 0) return undefined;
  const total = g.alreadyAffected + n;
  if (total <= CONFIRM_THRESHOLD) return undefined;
  if (userConfirmed(g)) return undefined;
  const what = g.alreadyAffected > 0
    ? `this ${tool} call would bring the objects modified or erased in this turn to ${total} (${g.alreadyAffected} already + ${n} now; more than ${CONFIRM_THRESHOLD})`
    : `this ${tool} call would modify or erase ${n} existing objects (more than ${CONFIRM_THRESHOLD})`;
  return (
    `Confirmation required: ${what}. ` +
    'Do not retry now and do not split the change into smaller calls. Tell the user in Vietnamese how many objects and what will change, ask "Cậu có đồng ý không?", ' +
    'and end your turn. Run the operation only after the user confirms in their next message.'
  );
}
