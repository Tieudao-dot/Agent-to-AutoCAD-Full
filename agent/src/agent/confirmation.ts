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

let anonymous = 0;

/**
 * The existing objects a call would modify/erase, by handle (batch_execute: all its calls). The same object
 * changed twice in a turn (move, then change layer) counts once. A call without a handle gets a unique key.
 */
export function affectedHandles(tool: string, params: Record<string, unknown>): string[] {
  if (tool === 'batch_execute' && Array.isArray(params.calls)) {
    const out: string[] = [];
    for (const c of params.calls as Array<{ tool?: unknown; params?: unknown }>) {
      if (c && typeof c.tool === 'string') out.push(...affectedHandles(c.tool, (c.params as Record<string, unknown>) ?? {}));
    }
    return out;
  }
  const n = affectedCount(tool, params);
  if (n === 0) return [];
  const hs = Array.isArray(params.handles) ? (params.handles as unknown[]).filter((h): h is string => typeof h === 'string') : [];
  if (hs.length > 0) return hs.map((h) => h.toUpperCase());
  if (typeof params.handle === 'string') return [params.handle.toUpperCase()];
  return Array.from({ length: n }, () => `#${tool}-${++anonymous}`);
}

/** Handles a successful call created (draw_*, insert_block, copy / array / offset, a batch's creating calls). */
export function createdHandles(tool: string, params: Record<string, unknown>, result: unknown): string[] {
  const out: string[] = [];
  const take = (r: unknown) => {
    if (!r || typeof r !== 'object') return;
    const o = r as Record<string, unknown>;
    if (Array.isArray(o.handles)) for (const h of o.handles) if (typeof h === 'string') out.push(h.toUpperCase());
    if (typeof o.handle === 'string') out.push(o.handle.toUpperCase());
  };
  if (tool === 'batch_execute' && Array.isArray(params.calls)) {
    const results = (result as { results?: unknown[] } | undefined)?.results;
    if (!Array.isArray(results)) return out;
    (params.calls as Array<{ tool?: unknown; params?: unknown }>).forEach((c, i) => {
      const r = results[i] as { ok?: unknown; result?: unknown } | undefined;
      if (c && typeof c.tool === 'string' && r?.ok === true && affectedCount(c.tool, (c.params as Record<string, unknown>) ?? {}) === 0) take(r.result);
    });
    return out;
  }
  if (affectedCount(tool, params) === 0) take(result);
  return out;
}

const PHRASES = [
  'đồng ý', 'dong y', 'xác nhận', 'xac nhan', 'làm đi', 'lam di', 'cứ làm', 'cu lam', 'tiếp tục', 'tiep tuc',
  'chắc chắn', 'chac chan', 'xóa đi', 'xoa di', 'xóa luôn', 'xoa luon', 'được rồi', 'duoc roi', 'go ahead', 'đổi đi', 'sửa đi',
].map((p) => p.split(' '));
/** Accepted among the first words of a short reply. */
const ANYWHERE = new Set(['ok', 'okay', 'oke', 'okie', 'yes', 'yep', 'confirm']);
/** Accepted only as the first word ("có, xóa đi" yes; "có bao nhiêu..." / "có thể xóa..." no). */
const LEADING = new Set(['có', 'ừ', 'ừm', 'uh', 'y', 'đúng', 'được', 'duoc', 'chuẩn', 'vâng', 'dạ']);
/** A refusal when among the first words ("Đừng tiếp tục", "Chưa đồng ý", "khỏi xóa"); "không sao" is not one. */
const NEGATIONS = new Set(['không', 'khong', 'đừng', 'dừng', 'chưa', 'chua', 'khỏi', 'khoi', 'hủy', 'huy', 'no', 'nope']);
const FIRST_WORDS = 3;

/**
 * True if the user's message reads as an explicit confirmation: a short reply (≤ 12 words), not a question,
 * no refusal word among its first words, and a yes-word or phrase starting within its first three words, matched
 * as whole words ("xóa điểm cao độ" is not "xóa đi").
 */
export function isConfirmation(text: string | undefined): boolean {
  if (!text) return false;
  const t = text.toLowerCase().normalize('NFC').trim();
  if (t.endsWith('?')) return false; // "…thì ok không?" asks back
  const tokens = t.split(/[\s,.!?;:()"'…-]+/u).filter(Boolean);
  if (tokens.length === 0 || tokens.length > 12) return false; // confirmations are short replies
  for (let i = 0; i < Math.min(tokens.length, FIRST_WORDS + 1); i++) {
    if (NEGATIONS.has(tokens[i]!) && !(tokens[i] === 'không' && tokens[i + 1] === 'sao') && !(tokens[i] === 'khong' && tokens[i + 1] === 'sao')) return false;
  }
  for (let i = 0; i < Math.min(tokens.length, FIRST_WORDS); i++) {
    if (PHRASES.some((ph) => ph.every((w, k) => tokens[i + k] === w))) return true;
    if (ANYWHERE.has(tokens[i]!)) return true;
  }
  const first = tokens[0]!;
  if (!LEADING.has(first)) return false;
  // "có thể ...?" / "có bao nhiêu ...?" are questions, not answers
  return !(first === 'có' && /^(thể|the|bao|phải|nên|cần)$/u.test(tokens[1] ?? ''));
}

export interface GateState {
  /** The user's message of the current turn. */
  userText: string | undefined;
  /** The previous turn asked the user something (a "?" in Claude's reply, a card, or this gate refused a call). */
  answersQuestion: boolean;
  /** The turn was started by the "Đồng ý" button of a confirmation card or the "Làm" button of a plan card. */
  confirmedByButton?: boolean;
  /** Existing objects already changed or removed by earlier calls of this turn (distinct handles). */
  touched?: ReadonlySet<string>;
  /** Objects created in this turn: changing them never needs a confirmation. */
  created?: ReadonlySet<string>;
  /** Older callers: a plain count instead of the two sets. */
  alreadyAffected?: number;
}

/** True when the user's message counts as a "yes" to a question Claude asked. */
export function userConfirmed(g: GateState): boolean {
  return !!g.confirmedByButton || (g.answersQuestion && isConfirmation(g.userText));
}

/** The existing objects this call adds to the turn's count (not yet counted, not created in this turn). */
export function newlyAffected(tool: string, params: Record<string, unknown>, g: GateState): string[] {
  const seen = new Set<string>();
  return affectedHandles(tool, params).filter((h) => {
    if (seen.has(h) || g.touched?.has(h) || g.created?.has(h)) return false;
    seen.add(h);
    return true;
  });
}

/**
 * Returns an error message if the call must be confirmed first, otherwise undefined.
 * The count is cumulative over the turn: distinct existing objects (g.touched) + those this call adds.
 */
export function confirmationError(tool: string, params: Record<string, unknown>, g: GateState): string | undefined {
  const add = newlyAffected(tool, params, g).length;
  if (add === 0) return undefined;
  const before = g.touched ? g.touched.size : g.alreadyAffected ?? 0;
  const total = before + add;
  if (total <= CONFIRM_THRESHOLD) return undefined;
  if (userConfirmed(g)) return undefined;
  const what = before > 0
    ? `this ${tool} call would bring the existing objects modified or erased in this turn to ${total} (${before} already + ${add} now; more than ${CONFIRM_THRESHOLD})`
    : `this ${tool} call would modify or erase ${add} existing objects (more than ${CONFIRM_THRESHOLD})`;
  return (
    `Confirmation required: ${what}. ` +
    'Do not retry now and do not split the change into smaller calls. Tell the user in Vietnamese how many objects and what will change, ask "Cậu có đồng ý không?", ' +
    'and end your turn. Run the operation only after the user confirms in their next message.'
  );
}
