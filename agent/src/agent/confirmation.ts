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
  // KT-37 (12/11/2026, a small change of the 2D rule): these also change or remove existing objects
  fillet_entities: (p) => (typeof p.handle1 === 'string' ? 1 : 0) + (typeof p.handle2 === 'string' ? 1 : 0),
  chamfer_entities: (p) => (typeof p.handle1 === 'string' ? 1 : 0) + (typeof p.handle2 === 'string' ? 1 : 0),
  define_block: (p) => (p.replace_with_insert === false ? 0 : len(p.entity_handles)),
  delete_layout: () => 1,
  // 3D: a boolean changes its first / target solid and erases the others (unless keep_tools)
  solid_union: (p) => len(p.handles),
  solid_intersect: (p) => len(p.handles),
  solid_subtract: (p) => (typeof p.target === 'string' ? 1 : 0) + (p.keep_tools === true ? 0 : len(p.subtract)),
  // extrude / revolve only change existing objects when they erase the profile
  extrude: (p) => (p.erase_profile === true ? len(p.profile_handles) : 0),
  revolve: (p) => (p.erase_profile === true ? len(p.profile_handles) : 0),
  // 3D phase 2: sweep / loft only change existing objects when they erase the profile / sections
  sweep: (p) => (p.erase_profile === true ? len(p.profile_handles) : 0),
  loft: (p) => (p.erase_profile === true ? len(p.section_handles) : 0),
  // editing an existing solid (always confirmed when it is older than the turn, see ALWAYS_CONFIRM)
  slice_solid: (p) => (typeof p.handle === 'string' ? 1 : 0),
  fillet_edges: (p) => (typeof p.handle === 'string' ? 1 : 0),
  chamfer_edges: (p) => (typeof p.handle === 'string' ? 1 : 0),
  shell_solid: (p) => (typeof p.handle === 'string' ? 1 : 0),
  // 3D transforms: the same rule as move / rotate / mirror (more than 20 existing objects)
  rotate_3d: (p) => len(p.handles),
  align: (p) => len(p.handles),
  mirror_3d: (p) => (p.keep_original === false ? len(p.handles) : 0),
  // nâng cấp đợt B: the 2D rule (more than 20 existing objects). stretch_entities without handles: the plugin itself
  // refuses a window that would change more than 20 objects and lists them, so Claude asks and calls again with handles.
  stretch_entities: (p) => len(p.handles),
  break_entity: (p) => (typeof p.handle === 'string' ? 1 : 0),
  lengthen_entity: (p) => (typeof p.handle === 'string' ? 1 : 0),
  match_properties: (p) => len(p.handles),
  // nâng cấp đợt C: replacing text changes existing objects (step 2, with handles); the preview changes nothing.
  // It always needs the card's Đồng ý (REPLACE_TOOLS below), whatever the count.
  find_replace_text: (p) => len(p.handles),
};

/**
 * 3D booleans: changing or erasing a solid that existed before this turn always needs the user's yes, whatever the
 * count (a union / subtract silently replaces the user's solid). Solids built in this turn need none.
 */
export const ALWAYS_CONFIRM = new Set([
  'solid_union', 'solid_subtract', 'solid_intersect',
  // 3D phase 2: slicing, filleting, chamfering or shelling an existing solid (decided 12/11/2026)
  'slice_solid', 'fillet_edges', 'chamfer_edges', 'shell_solid',
]);

/**
 * Tools that create new objects even when they also change existing ones (extrude / revolve / sweep / loft with
 * erase_profile; slice_solid keeping both halves). Their result handles minus the handles they change are "created".
 */
const CREATES_TOO = new Set(['extrude', 'revolve', 'sweep', 'loft', 'slice_solid', 'fillet_entities', 'chamfer_entities', 'define_block']);

/** The handles a destructive call names, for the tools whose handles are not in 'handles' / 'handle'. */
function namedHandles(tool: string, params: Record<string, unknown>): string[] | undefined {
  const strs = (v: unknown) => (Array.isArray(v) ? v.filter((h): h is string => typeof h === 'string') : []);
  if (tool === 'solid_subtract') {
    return [...(typeof params.target === 'string' ? [params.target] : []), ...(params.keep_tools === true ? [] : strs(params.subtract))];
  }
  if (tool === 'extrude' || tool === 'revolve' || tool === 'sweep') return params.erase_profile === true ? strs(params.profile_handles) : [];
  if (tool === 'loft') return params.erase_profile === true ? strs(params.section_handles) : [];
  if (tool === 'fillet_entities' || tool === 'chamfer_entities') return [params.handle1, params.handle2].filter((h): h is string => typeof h === 'string');
  if (tool === 'define_block') return params.replace_with_insert === false ? [] : strs(params.entity_handles);
  return undefined;
}

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
  const named = namedHandles(tool, params);
  if (named && named.length > 0) return named.map((h) => h.toUpperCase());
  const hs = Array.isArray(params.handles) ? (params.handles as unknown[]).filter((h): h is string => typeof h === 'string') : [];
  if (hs.length > 0) return hs.map((h) => h.toUpperCase());
  if (typeof params.handle === 'string') return [params.handle.toUpperCase()];
  return Array.from({ length: n }, () => `#${tool}-${++anonymous}`);
}

/**
 * Test & sửa đợt B (NB-1): stretch_entities without handles changes whatever lies in its crossing window. The gate cannot
 * count it before the call, so (1) the plugin refuses a window that would change more objects than the turn has left
 * under the 20-object rule (WINDOW_LIMIT_PARAM, set by the assistant, never by Claude), and (2) after the call its result
 * handles count as existing objects changed in this turn (never as created ones: a moved 3D solid stays "existing").
 */
export const WINDOW_LIMIT_PARAM = 'max_unasked';
const WINDOW_TOOLS = new Set(['stretch_entities']);

function isWindowCall(tool: string, params: Record<string, unknown>): boolean {
  return WINDOW_TOOLS.has(tool) && !(Array.isArray(params.handles) && params.handles.length > 0);
}

/** Existing objects a successful window call changed (from its result; batch_execute: its window calls). */
export function windowChanged(tool: string, params: Record<string, unknown>, result: unknown): string[] {
  const out: string[] = [];
  const take = (r: unknown) => {
    const hs = (r as { handles?: unknown } | null | undefined)?.handles;
    if (Array.isArray(hs)) for (const h of hs) if (typeof h === 'string') out.push(h.toUpperCase());
  };
  if (tool === 'batch_execute' && Array.isArray(params.calls)) {
    const results = (result as { results?: unknown[] } | undefined)?.results;
    if (!Array.isArray(results)) return out;
    (params.calls as Array<{ tool?: unknown; params?: unknown }>).forEach((c, i) => {
      const r = results[i] as { ok?: unknown; result?: unknown } | undefined;
      const cp = (c?.params as Record<string, unknown>) ?? {};
      if (c && typeof c.tool === 'string' && r?.ok === true && isWindowCall(c.tool, cp)) take(r.result);
    });
    return [...new Set(out)];
  }
  if (isWindowCall(tool, params)) take(result);
  return [...new Set(out)];
}

/** The params with the window limit the assistant computed (any value Claude put there is dropped). */
function windowLimited(params: Record<string, unknown>, limit: number | undefined): Record<string, unknown> {
  const { [WINDOW_LIMIT_PARAM]: _ignored, ...rest } = params;
  return limit === undefined ? rest : { ...rest, [WINDOW_LIMIT_PARAM]: limit };
}

/**
 * How many objects a window call of this turn may still change without asking: 20 minus the existing objects the turn
 * has already changed (the gate added this call's named handles before it is sent), shared between the window calls
 * of one batch. undefined = the plugin's own limit (20), when the user has already said yes in this turn.
 */
function windowLimit(g: GateState, windowCalls: number): number | undefined {
  if (userConfirmed(g)) return undefined;
  const before = g.touched ? g.touched.size : g.alreadyAffected ?? 0;
  return Math.floor(Math.max(0, CONFIRM_THRESHOLD - before) / Math.max(1, windowCalls));
}

/** Handles a successful call created (draw_*, insert_block, copy / array / offset, a batch's creating calls). */
export function createdHandles(tool: string, params: Record<string, unknown>, result: unknown): string[] {
  const out: string[] = [];
  const take = (r: unknown, t: string, p: Record<string, unknown>) => {
    if (!r || typeof r !== 'object') return;
    const o = r as Record<string, unknown>;
    // a tool that changes some objects and creates others (slice_solid): the changed ones are not new
    const changed = new Set(CREATES_TOO.has(t) ? affectedHandles(t, p) : []);
    const add = (h: string) => {
      const u = h.toUpperCase();
      if (!changed.has(u)) out.push(u);
    };
    if (Array.isArray(o.handles)) for (const h of o.handles) if (typeof h === 'string') add(h);
    if (typeof o.handle === 'string') add(o.handle);
  };
  if (tool === 'batch_execute' && Array.isArray(params.calls)) {
    const results = (result as { results?: unknown[] } | undefined)?.results;
    if (!Array.isArray(results)) return out;
    (params.calls as Array<{ tool?: unknown; params?: unknown }>).forEach((c, i) => {
      const r = results[i] as { ok?: unknown; result?: unknown } | undefined;
      const cp = (c?.params as Record<string, unknown>) ?? {};
      if (c && typeof c.tool === 'string' && r?.ok === true && !WINDOW_TOOLS.has(c.tool) && (CREATES_TOO.has(c.tool) || affectedCount(c.tool, cp) === 0)) take(r.result, c.tool, cp);
    });
    return out;
  }
  if (!WINDOW_TOOLS.has(tool) && (CREATES_TOO.has(tool) || affectedCount(tool, params) === 0)) take(result, tool, params);
  return [...new Set(out)];
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
  /**
   * The handles of the confirmation card whose "Đồng ý" button started this turn (upper case). Decided 12/11/2026 (C2):
   * changing, erasing or exploding 3D solids that existed before the turn is unlocked only by that button, and only
   * for these handles; a typed "ok" / "đồng ý" or the plan card's "Làm" does not unlock it (the 2D rule is unchanged).
   */
  cardConfirmed?: ReadonlySet<string>;
  /** Existing objects already changed or removed by earlier calls of this turn (distinct handles). */
  touched?: ReadonlySet<string>;
  /** Objects created in this turn: changing them never needs a confirmation. */
  created?: ReadonlySet<string>;
  /** Older callers: a plain count instead of the two sets. */
  alreadyAffected?: number;
  /**
   * Nâng cấp đợt A, layer names of build_3d_from_plan (see unconfirmedPlanLayers): the text of the confirmation card /
   * plan card this turn answers, the user's messages of this conversation (the current one included; without it only
   * userText is read), and the layers approved earlier in this conversation (upper case).
   */
  askedText?: string;
  userTexts?: readonly string[];
  planLayersOk?: ReadonlySet<string>;
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
/** Solids that existed before this turn and that a 3D boolean / solid edit of this call (or of a batch) would change or erase. */
export function existingSolidsChanged(tool: string, params: Record<string, unknown>, g: GateState): string[] {
  if (tool === 'batch_execute' && Array.isArray(params.calls)) {
    const out: string[] = [];
    for (const c of params.calls as Array<{ tool?: unknown; params?: unknown }>) {
      if (c && typeof c.tool === 'string' && c.tool !== 'batch_execute') out.push(...existingSolidsChanged(c.tool, (c.params as Record<string, unknown>) ?? {}, g));
    }
    return [...new Set(out)];
  }
  if (!ALWAYS_CONFIRM.has(tool)) return [];
  return [...new Set(affectedHandles(tool, params).filter((h) => !h.startsWith('#') && !g.created?.has(h)))];
}

/**
 * explode_entities / erase_entities on 3D solids, surfaces or meshes that existed before the turn need the Đồng ý button
 * of a confirmation card listing them (decided 12/11/2026: C1 for erase, C2 for "only the card"). Only the plugin knows an
 * object's type, so the plugin refuses them ("Confirmation required") unless the call carries this parameter: the handles
 * the user approved on the card, which the assistant adds. Claude cannot set it: it is not in the tools' schemas and
 * applyUserConfirmation replaces whatever a call carries.
 */
export const USER_CONFIRMED_PARAM = 'user_confirmed';
/** Tools whose 3D-solid confirmation the plugin checks itself (by object type), with USER_CONFIRMED_PARAM. */
export const PLUGIN_CONFIRMED = new Set(['explode_entities', 'erase_entities']);

/**
 * Nâng cấp đợt C (13/11/2026): find_replace_text changes many existing texts at once, so replacing (a call with handles)
 * always needs the Đồng ý button of a confirmation card that listed those handles, whatever the count; a typed yes or the
 * plan card's "Làm" does not. The plugin checks it too (user_confirmed, added by applyUserConfirmation).
 */
export const REPLACE_TOOLS = new Set(['find_replace_text']);
/** Tools that get user_confirmed (the handles on the approved card): the 3D ones above and find_replace_text. */
const CARD_PARAM_TOOLS = new Set([...PLUGIN_CONFIRMED, ...REPLACE_TOOLS]);

/** The handles of a replace call (or of the replace calls in a batch) that are not on the card the user approved. */
export function replaceNotApproved(tool: string, params: Record<string, unknown>, g: GateState): string[] {
  if (tool === 'batch_execute' && Array.isArray(params.calls)) {
    const out: string[] = [];
    for (const c of params.calls as Array<{ tool?: unknown; params?: unknown }>) {
      if (c && typeof c.tool === 'string' && c.tool !== 'batch_execute') out.push(...replaceNotApproved(c.tool, (c.params as Record<string, unknown>) ?? {}, g));
    }
    return [...new Set(out)];
  }
  if (!REPLACE_TOOLS.has(tool) || !Array.isArray(params.handles)) return [];
  const hs = (params.handles as unknown[]).filter((h): h is string => typeof h === 'string').map((h) => h.toUpperCase());
  return [...new Set(hs.filter((h) => !g.cardConfirmed?.has(h)))];
}

/** The params with user_confirmed = the call's handles that are on the approved card (no parameter when none are). */
function flagged(params: Record<string, unknown>, g: GateState): Record<string, unknown> {
  const { [USER_CONFIRMED_PARAM]: _ignored, ...rest } = params;
  const hs = Array.isArray(params.handles) ? (params.handles as unknown[]).filter((h): h is string => typeof h === 'string') : [];
  const ok = [...new Set(hs.map((h) => h.toUpperCase()))].filter((h) => g.cardConfirmed?.has(h));
  return ok.length > 0 ? { ...rest, [USER_CONFIRMED_PARAM]: ok } : rest;
}

/**
 * The params actually sent to the plugin: explode_entities / erase_entities (alone or inside batch_execute) carry
 * user_confirmed = their handles that the user approved on the confirmation card of this turn, and nothing otherwise.
 */
export function applyUserConfirmation(tool: string, params: Record<string, unknown>, g: GateState): Record<string, unknown> {
  if (CARD_PARAM_TOOLS.has(tool)) return flagged(params, g);
  if (WINDOW_TOOLS.has(tool)) return windowLimited(params, isWindowCall(tool, params) ? windowLimit(g, 1) : undefined);
  if (tool === 'batch_execute' && Array.isArray(params.calls)) {
    let changed = false;
    const paramsOf = (call: { params?: unknown }) => (call.params && typeof call.params === 'object' ? (call.params as Record<string, unknown>) : {});
    const windows = (params.calls as unknown[]).filter((c) => {
      const call = c as { tool?: unknown; params?: unknown } | null;
      return !!call && typeof call === 'object' && typeof call.tool === 'string' && isWindowCall(call.tool, paramsOf(call));
    }).length;
    const calls = (params.calls as unknown[]).map((c) => {
      const call = c as { tool?: unknown; params?: unknown } | null;
      if (!call || typeof call !== 'object' || typeof call.tool !== 'string') return c;
      const p = paramsOf(call);
      if (WINDOW_TOOLS.has(call.tool)) {
        const next = windowLimited(p, isWindowCall(call.tool, p) ? windowLimit(g, windows) : undefined);
        if (!(WINDOW_LIMIT_PARAM in p) && !(WINDOW_LIMIT_PARAM in next)) return c;
        changed = true;
        return { ...call, params: next };
      }
      if (!CARD_PARAM_TOOLS.has(call.tool)) return c;
      const next = flagged(p, g);
      if (!(USER_CONFIRMED_PARAM in p) && !(USER_CONFIRMED_PARAM in next)) return c;
      changed = true;
      return { ...call, params: next };
    });
    return changed ? { ...params, calls } : params;
  }
  return params;
}

/** The plugin refused the call (or a call of the batch) until the user says yes: the turn now asks a question. */
export function refusedForConfirmation(resp: { ok: boolean; error?: { message?: string }; result?: unknown }): boolean {
  const asks = (m: unknown) => typeof m === 'string' && m.startsWith('Confirmation required');
  if (!resp.ok) return asks(resp.error?.message);
  const results = (resp.result as { results?: unknown[] } | undefined)?.results;
  return Array.isArray(results) && results.some((r) => {
    const x = r as { ok?: unknown; error?: { message?: unknown } } | null;
    return !!x && x.ok === false && asks(x.error?.message);
  });
}

/**
 * Nâng cấp đợt A (13/11/2026): build_3d_from_plan on layers with other names (TUONG, CUA, A-WALL-NEW…). Claude may
 * GUESS which layer is a wall / door / window / slab / column (the plugin reports layer_guess), but must ask the user
 * before building from the guess. A build (not a dry_run) with a layer that is not a default one is refused unless
 *  - the user wrote that layer name in a message of this conversation that talks about layers ("tường ở layer TUONG"), or
 *  - this turn answers a confirmation card / plan card whose text names the layer (its Đồng ý / Làm button, or a typed
 *    yes to it), or
 *  - the layer was approved that way earlier in this conversation (storey 2 does not ask again).
 * So the question is always asked with the palette's own card, and the yes is a yes to these very layers.
 * Reading (dry_run) is always allowed.
 */
export const PLAN_LAYER_PARAMS = ['wall_layers', 'door_layers', 'window_layers', 'slab_layers', 'column_layers'] as const;
export const PLAN_DEFAULT_LAYERS: ReadonlySet<string> = new Set(['A-WALL', 'A-DOOR', 'A-WIND', 'A-SLAB', 'A-FLOR', 'A-COLS', 'A-COL']);

/** The layer names (or patterns) of a build_3d_from_plan call, or of the builds in a batch, that are not default ones. */
export function planLayersOf(tool: string, params: Record<string, unknown>): string[] {
  if (tool === 'batch_execute' && Array.isArray(params.calls)) {
    const out: string[] = [];
    for (const c of params.calls as Array<{ tool?: unknown; params?: unknown }>) {
      if (c && typeof c.tool === 'string' && c.tool !== 'batch_execute') out.push(...planLayersOf(c.tool, (c.params as Record<string, unknown>) ?? {}));
    }
    return [...new Set(out)];
  }
  if (tool !== 'build_3d_from_plan' || params.dry_run === true) return [];
  const out: string[] = [];
  for (const k of PLAN_LAYER_PARAMS) {
    // the plugin also reads a single string as a one-layer list (Params.Strings); inside batch_execute no schema stops it
    const v = typeof params[k] === 'string' ? [params[k]] : params[k];
    if (!Array.isArray(v)) continue;
    for (const x of v) {
      if (typeof x !== 'string' || !x.trim()) continue;
      if (!PLAN_DEFAULT_LAYERS.has(x.trim().toUpperCase())) out.push(x.trim());
    }
  }
  return [...new Set(out)];
}

/** Characters of a layer name: a name is matched as a whole token ("CUA" is not named by "CUA-SO" or "cuadi"). */
const LAYER_CHAR = '[\\p{L}\\p{N}_$-]';
/** A message that talks about layers: only there a word like "tuong" can be the layer TUONG and not the word "tường" typed without marks. */
const LAYER_WORD = /(?<![\p{L}\p{N}])(?:layers?|lớp)(?![\p{L}\p{N}])/iu;

/**
 * Matches a layer name written out in a text, in any letter case. A pattern ("KT_TUONG*") matches the pattern itself or a
 * name it covers; a pattern with fewer than 3 fixed characters ("*", "T*") is never taken as written.
 */
export function layerNameRegex(layer: string): RegExp | undefined {
  const name = layer.normalize('NFC').trim();
  const wild = /[*?]/.test(name);
  if (name.replace(/[*?\s]/g, '').length < (wild ? 3 : 1)) return undefined;
  const body = name
    .split(/([*?])/)
    .map((part) => (part === '*' ? `${LAYER_CHAR}*` : part === '?' ? LAYER_CHAR : part.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')))
    .join('');
  return new RegExp(`(?<!${LAYER_CHAR})${body}(?!${LAYER_CHAR})`, 'iu');
}

/** The layers of a build_3d_from_plan call (or of a batch) that the user has neither named nor approved on a card. */
export function unconfirmedPlanLayers(tool: string, params: Record<string, unknown>, g: GateState): string[] {
  const layers = planLayersOf(tool, params);
  if (layers.length === 0) return [];
  const texts = (g.userTexts ?? (g.userText ? [g.userText] : [])).map((t) => t.normalize('NFC')).filter((t) => LAYER_WORD.test(t));
  const asked = userConfirmed(g) ? (g.askedText ?? '').normalize('NFC') : '';
  return layers.filter((name) => {
    if (g.planLayersOk?.has(name.toUpperCase())) return false;
    const rx = layerNameRegex(name);
    if (!rx) return true;
    return !(texts.some((t) => rx.test(t)) || (asked !== '' && rx.test(asked)));
  });
}

export function confirmationError(tool: string, params: Record<string, unknown>, g: GateState): string | undefined {
  const guessed = unconfirmedPlanLayers(tool, params, g);
  if (guessed.length > 0) {
    return (
      `Confirmation required: this ${tool} call builds from layers the user has not confirmed (${guessed.slice(0, 20).join(', ')}). ` +
      'A layer mapping you worked out yourself must be approved on a card first. Do not retry now and do not work around it (no wall_handles collected from those layers either). ' +
      'Call request_confirmation with one Vietnamese sentence that writes every layer name exactly as in the drawing and what it is ' +
      '(e.g. "Dựng 3D với: tường = TUONG, cửa đi = CUA, cửa sổ = CUASO?"), and end your turn. ' +
      'Build after the user presses Đồng ý, with exactly the layers named on that card.'
    );
  }
  const texts = replaceNotApproved(tool, params, g);
  if (texts.length > 0) {
    return (
      `Confirmation required: find_replace_text would change ${texts.length} existing text object(s) that are not on a confirmation card the user approved. ` +
      'Replacing always needs the Đồng ý button of a card listing them; a typed "ok" / "đồng ý" does not count. Do not retry now and do not work around it ' +
      '(no edit_text / set_block_attributes one by one). Run find_replace_text without handles (preview), call request_confirmation with its match_handles and ' +
      'one Vietnamese sentence (count, find → replace, one example), and end your turn. Replace with exactly those handles after the user presses Đồng ý.'
    );
  }
  // C2 (12/11/2026): only the Đồng ý button of a card that listed these solids unlocks them, not a typed yes
  const solids = existingSolidsChanged(tool, params, g).filter((h) => !g.cardConfirmed?.has(h));
  if (solids.length > 0) {
    return (
      `Confirmation required: this ${tool} call would change or erase ${solids.length} 3D solid(s) that were already in the drawing before this turn ` +
      `(${solids.slice(0, 20).join(', ')}${solids.length > 20 ? ', …' : ''}). Only the Đồng ý button of a confirmation card that lists these handles ` +
      'allows it; a typed "ok" / "đồng ý" or the plan card\'s "Làm" does not. Do not retry now and do not work around it. ' +
      'Call request_confirmation now with exactly these handles and one Vietnamese sentence saying what will change (e.g. "Trừ 2 lỗ cửa khỏi tường 2F4A?"), ' +
      'and end your turn. Run the operation only after the user presses Đồng ý.'
    );
  }
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
