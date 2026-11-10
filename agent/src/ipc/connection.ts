import { EventEmitter } from 'node:events';
import net from 'node:net';
import type { Duplex } from 'node:stream';
import {
  LineDecoder,
  encodeMessage,
  parsePluginMessage,
  type PluginToSidecar,
  type SidecarToPlugin,
} from './protocol.js';

export interface ConnectionEvents {
  message: [PluginToSidecar];
  invalid: [Error, string];
  close: [string];
}

/** Windows named pipe path for a pipe name such as "claudecad-1234". */
export function pipePath(name: string): string {
  if (name.startsWith('\\\\') || name.startsWith('/')) return name; // already a full path / unix socket
  return process.platform === 'win32' ? `\\\\.\\pipe\\${name}` : `/tmp/${name}.sock`;
}

/**
 * JSON-Lines framing on top of any duplex stream (named pipe client socket in
 * production, in-memory or unix sockets in tests).
 */
export class Connection extends EventEmitter<ConnectionEvents> {
  private readonly decoder = new LineDecoder();
  private closed = false;

  constructor(private readonly stream: Duplex) {
    super();
    stream.on('data', (chunk: Buffer) => this.onData(chunk));
    stream.on('end', () => this.close('pipe closed by plugin'));
    stream.on('close', () => this.close('pipe closed'));
    stream.on('error', (err) => this.close(`pipe error: ${err.message}`));
  }

  get isOpen(): boolean {
    return !this.closed;
  }

  private onData(chunk: Buffer): void {
    let lines: string[];
    try {
      lines = this.decoder.push(chunk);
    } catch (err) {
      this.emit('invalid', err as Error, '');
      return;
    }
    for (const line of lines) {
      let parsed: unknown;
      try {
        parsed = JSON.parse(line);
      } catch (err) {
        this.emit('invalid', err as Error, line);
        continue;
      }
      let msg;
      try {
        msg = parsePluginMessage(parsed);
      } catch (err) {
        this.emit('invalid', err as Error, line);
        continue;
      }
      try {
        this.emit('message', msg);
      } catch (err) {
        // a failing handler must not drop the other lines of this chunk
        process.emitWarning(`message handler failed: ${(err as Error).message}`);
      }
    }
  }

  /** Writes one message. Returns false when the connection is closed. */
  send(msg: SidecarToPlugin): boolean {
    if (this.closed) return false;
    try {
      this.stream.write(encodeMessage(msg), 'utf8');
      return true;
    } catch {
      return false;
    }
  }

  close(reason = 'closed'): void {
    if (this.closed) return;
    this.closed = true;
    try {
      this.stream.end();
    } catch {
      /* ignore */
    }
    this.emit('close', reason);
  }
}

/** Connects to the plugin's pipe server, retrying while the server is not listening yet. */
export async function connectPipe(name: string, attempts = 50, delayMs = 200): Promise<Connection> {
  const path = pipePath(name);
  let lastErr: unknown;
  for (let i = 0; i < attempts; i++) {
    try {
      const socket = await new Promise<net.Socket>((resolve, reject) => {
        const s = net.connect(path);
        s.once('connect', () => {
          s.off('error', reject);
          resolve(s);
        });
        s.once('error', reject);
      });
      socket.setNoDelay(true);
      return new Connection(socket);
    } catch (err) {
      lastErr = err;
      await new Promise((r) => setTimeout(r, delayMs));
    }
  }
  throw new Error(`cannot connect to pipe ${path}: ${(lastErr as Error)?.message ?? lastErr}`);
}
