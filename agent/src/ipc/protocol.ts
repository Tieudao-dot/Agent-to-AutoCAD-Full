/**
 * ClaudeCAD IPC protocol (spec §6): JSON Lines over a named pipe, UTF-8 without BOM.
 * One JSON object per line, in both directions.
 */
import { StringDecoder } from 'node:string_decoder';

export const ERROR_CODES = [
  'INVALID_PARAMS',
  'ENTITY_NOT_FOUND',
  'LAYER_NOT_FOUND',
  'BLOCK_NOT_FOUND',
  'LOCKED',
  'ACAD_ERROR',
  'TIMEOUT',
  'TOO_MANY_ENTITIES',
] as const;
export type ErrorCode = (typeof ERROR_CODES)[number] | (string & {});

// ---------------------------------------------------------------- sidecar -> plugin

export interface ToolCallMessage {
  id: string;
  type: 'tool_call';
  tool: string;
  params: Record<string, unknown>;
}

export interface AgentTextMessage {
  type: 'agent_text';
  delta: string;
}

/** Closes the current text block so the palette starts a new bubble for the next text. */
export interface AgentTextEndMessage {
  type: 'agent_text_end';
}

export interface ToolStatusMessage {
  type: 'tool_status';
  tool: string;
  state: 'running' | 'done' | 'error';
}

export interface TurnEndMessage {
  type: 'turn_end';
  /** Set when the turn failed (not when it was cancelled by the user). */
  error?: string;
  cancelled?: boolean;
}

/** Extension: non-fatal error to show in the palette. */
export interface AgentErrorMessage {
  type: 'agent_error';
  message: string;
}

/** Extension: sidecar is connected and the agent session is configured. */
export interface ReadyMessage {
  type: 'ready';
  info: string;
  version: string;
}

/** Extension: confirmation that new_session started a fresh conversation. */
export interface SessionResetMessage {
  type: 'session_reset';
}

export type SidecarToPlugin =
  | ToolCallMessage
  | AgentTextMessage
  | AgentTextEndMessage
  | ToolStatusMessage
  | TurnEndMessage
  | AgentErrorMessage
  | ReadyMessage
  | SessionResetMessage;

// ---------------------------------------------------------------- plugin -> sidecar

export interface UserMessage {
  type: 'user_message';
  text: string;
  /** File name of the active drawing (informational). */
  drawing?: string;
}

export interface CancelMessage {
  type: 'cancel';
}

export interface NewSessionMessage {
  type: 'new_session';
}

export interface ToolError {
  code: ErrorCode;
  message: string;
}

export interface ToolResponseOk {
  id: string;
  ok: true;
  result: unknown;
}

export interface ToolResponseErr {
  id: string;
  ok: false;
  error: ToolError;
}

export type ToolResponse = ToolResponseOk | ToolResponseErr;

export type PluginToSidecar = UserMessage | CancelMessage | NewSessionMessage | ToolResponse;

// ---------------------------------------------------------------- encode / decode

/** Serializes one message as a single JSON line (terminated by "\n"). */
export function encodeMessage(msg: unknown): string {
  const json = JSON.stringify(msg);
  if (json === undefined) throw new TypeError('message is not JSON-serializable');
  // JSON.stringify never emits raw newlines inside strings, so the line is safe.
  return json + '\n';
}

export class ProtocolError extends Error {}

/**
 * Validates and classifies a parsed JSON value coming from the plugin.
 * Throws ProtocolError for anything that is not a known message.
 */
export function parsePluginMessage(value: unknown): PluginToSidecar {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    throw new ProtocolError('message must be a JSON object');
  }
  const m = value as Record<string, unknown>;

  if (typeof m.id === 'string' && typeof m.ok === 'boolean') {
    if (m.ok) return { id: m.id, ok: true, result: m.result ?? null };
    const err = m.error as Record<string, unknown> | undefined;
    if (!err || typeof err.code !== 'string' || typeof err.message !== 'string') {
      throw new ProtocolError(`response ${m.id}: error must be {code, message}`);
    }
    return { id: m.id, ok: false, error: { code: err.code, message: err.message } };
  }

  switch (m.type) {
    case 'user_message':
      if (typeof m.text !== 'string') throw new ProtocolError('user_message.text must be a string');
      return {
        type: 'user_message',
        text: m.text,
        ...(typeof m.drawing === 'string' ? { drawing: m.drawing } : {}),
      };
    case 'cancel':
      return { type: 'cancel' };
    case 'new_session':
      return { type: 'new_session' };
    default:
      throw new ProtocolError(`unknown message type: ${JSON.stringify(m.type)}`);
  }
}

/**
 * Splits an incoming byte stream into JSON lines. Handles chunks that end in the
 * middle of a line or in the middle of a multi-byte UTF-8 character, CRLF line
 * endings and a leading BOM.
 */
export class LineDecoder {
  private readonly decoder = new StringDecoder('utf8');
  private buffer = '';
  private first = true;

  constructor(private readonly maxLineLength = 16 * 1024 * 1024) {}

  /** Feeds a chunk; returns the complete lines it finished (without terminators). */
  push(chunk: Buffer | string): string[] {
    let text = typeof chunk === 'string' ? chunk : this.decoder.write(chunk);
    if (this.first && text.length > 0) {
      if (text.charCodeAt(0) === 0xfeff) text = text.slice(1);
      this.first = false;
    }
    this.buffer += text;
    const lines: string[] = [];
    let idx: number;
    while ((idx = this.buffer.indexOf('\n')) >= 0) {
      let line = this.buffer.slice(0, idx);
      this.buffer = this.buffer.slice(idx + 1);
      if (line.endsWith('\r')) line = line.slice(0, -1);
      if (line.trim().length > 0) lines.push(line);
    }
    if (this.buffer.length > this.maxLineLength) {
      this.buffer = '';
      throw new ProtocolError(`line longer than ${this.maxLineLength} characters`);
    }
    return lines;
  }

  /** Remaining partial line at end of stream (normally empty). */
  flush(): string[] {
    const rest = this.buffer + this.decoder.end();
    this.buffer = '';
    return rest.trim().length > 0 ? [rest] : [];
  }
}

/** Default per-request timeout (spec §6: 30 s). */
export const DEFAULT_TOOL_TIMEOUT_MS = 30_000;
