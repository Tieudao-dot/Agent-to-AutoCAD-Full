import {
  DEFAULT_TOOL_TIMEOUT_MS,
  type ToolCallMessage,
  type ToolResponse,
  type ToolResponseErr,
} from './protocol.js';

export type SendFn = (msg: ToolCallMessage) => boolean;

interface Pending {
  tool: string;
  resolve: (r: ToolResponse) => void;
  timer: NodeJS.Timeout;
}

/**
 * Request/response correlation for tool calls sent to the plugin.
 * `call()` never rejects: timeouts, disconnects and send failures are turned
 * into error responses so Claude gets a readable message instead of a hang.
 */
export class ToolRpc {
  private seq = 0;
  private readonly pending = new Map<string, Pending>();

  constructor(
    private send: SendFn,
    private readonly timeoutMs = DEFAULT_TOOL_TIMEOUT_MS,
  ) {}

  get pendingCount(): number {
    return this.pending.size;
  }

  nextId(): string {
    this.seq += 1;
    return `req-${this.seq}`;
  }

  call(tool: string, params: Record<string, unknown>, timeoutMs = timeoutFor(tool, params, this.timeoutMs)): Promise<ToolResponse> {
    const id = this.nextId();
    return new Promise<ToolResponse>((resolve) => {
      const timer = setTimeout(() => {
        if (this.pending.delete(id)) {
          resolve(errorResponse(id, 'TIMEOUT',
            `${tool} did not answer within ${Math.round(timeoutMs / 1000)} s. AutoCAD may be busy (a command or dialog is open), ` +
              'or the call is still running and may have COMPLETED. Do not simply run it again: first check the drawing ' +
              '(count_entities / list_entities on the layers involved) to see what was already created or changed, then do only what is missing. ' +
              'If nothing happened, ask the user to press Esc in AutoCAD and retry once with smaller batches.'));
        }
      }, timeoutMs);
      this.pending.set(id, { tool, resolve, timer });

      const sent = this.send({ id, type: 'tool_call', tool, params });
      if (!sent) {
        this.settle(id, errorResponse(id, 'ACAD_ERROR', 'Not connected to the AutoCAD plugin. Stop and tell the user the connection was lost.'));
      }
    });
  }

  /** Routes a response from the plugin. Returns false for unknown / late ids. */
  handleResponse(resp: ToolResponse): boolean {
    return this.settle(resp.id, resp);
  }

  private settle(id: string, resp: ToolResponse): boolean {
    const p = this.pending.get(id);
    if (!p) return false;
    clearTimeout(p.timer);
    this.pending.delete(id);
    p.resolve(resp);
    return true;
  }

  /** Fails every in-flight call (disconnect, cancel, document closed...). */
  failAll(code: string, message: string): void {
    for (const id of [...this.pending.keys()]) {
      this.settle(id, errorResponse(id, code, message));
    }
  }
}

export function errorResponse(id: string, code: string, message: string): ToolResponseErr {
  return { id, ok: false, error: { code, message } };
}

/** Calls that legitimately take long on a big drawing (render, plot, save, purge). */
const SLOW_TOOLS = new Set(['plot_pdf', 'save_drawing', 'purge_unused', 'regen', 'attach_xref', 'reload_xref', 'bind_xref', 'zoom_extents']);
const CAPTURE_TOOLS = new Set(['capture_view']);
/**
 * 3D builds that unite many boxes in one call (the walls of a plan, several storeys, a railing; nâng cấp đợt A): a
 * time-out while AutoCAD is still uniting would make Claude start the same build again. A dry run only reads.
 */
const BUILD_TOOLS = new Set(['build_3d_from_plan', 'build_railing']);
const slowBuild = (tool: unknown, params: unknown): boolean =>
  typeof tool === 'string' && BUILD_TOOLS.has(tool) && (params as { dry_run?: unknown } | undefined)?.dry_run !== true;
const MAX_TIMEOUT_MS = 5 * 60_000;

/**
 * Per-call timeout: one 30 s limit for everything made a 500-call batch or a PDF plot of a big drawing
 * "time out" while AutoCAD was still working — Claude then re-ran it and drew everything twice.
 */
export function timeoutFor(tool: string, params: Record<string, unknown>, base = DEFAULT_TOOL_TIMEOUT_MS): number {
  let ms = base;
  if (tool === 'batch_execute') {
    const calls = Array.isArray(params.calls) ? (params.calls as Array<{ tool?: unknown; params?: unknown }>) : [];
    ms = base + calls.length * 100;
    if (calls.some((c) => (typeof c?.tool === 'string' && (SLOW_TOOLS.has(c.tool) || CAPTURE_TOOLS.has(c.tool))) || slowBuild(c?.tool, c?.params))) ms = Math.max(ms, 120_000);
  } else if (SLOW_TOOLS.has(tool) || CAPTURE_TOOLS.has(tool) || slowBuild(tool, params)) {
    ms = Math.max(base, 120_000);
  }
  return Math.min(ms, Math.max(MAX_TIMEOUT_MS, base)); // a longer CLAUDECAD_TOOL_TIMEOUT_MS is respected
}
