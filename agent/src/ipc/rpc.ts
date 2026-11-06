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

  /** Swaps the transport (used after a reconnect). Pending calls are failed first. */
  setSender(send: SendFn): void {
    this.failAll('ACAD_ERROR', 'Connection to AutoCAD was reset while the call was in flight.');
    this.send = send;
  }

  get pendingCount(): number {
    return this.pending.size;
  }

  nextId(): string {
    this.seq += 1;
    return `req-${this.seq}`;
  }

  call(tool: string, params: Record<string, unknown>, timeoutMs = this.timeoutMs): Promise<ToolResponse> {
    const id = this.nextId();
    return new Promise<ToolResponse>((resolve) => {
      const timer = setTimeout(() => {
        if (this.pending.delete(id)) {
          resolve(errorResponse(id, 'TIMEOUT',
            `${tool} did not answer within ${Math.round(timeoutMs / 1000)} s. AutoCAD may be busy (a command or dialog is open). ` +
              'Ask the user to press Esc in AutoCAD, then retry once; for big jobs split the batch into smaller batches.'));
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
