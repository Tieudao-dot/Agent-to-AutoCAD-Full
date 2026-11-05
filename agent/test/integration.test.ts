/**
 * Integration tests of the IPC layer over a real socket (unix socket on
 * Linux/macOS, named pipe on Windows): a fake plugin server on one side, the
 * sidecar's Connection + ToolRpc + MCP tool bodies on the other.
 */
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import net from 'node:net';
import path from 'node:path';
import { after, describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { connectPipe, pipePath } from '../src/ipc/connection.js';
import { LineDecoder, encodeMessage } from '../src/ipc/protocol.js';
import { ToolRpc } from '../src/ipc/rpc.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';

const here = path.dirname(fileURLToPath(import.meta.url));

interface FakePlugin {
  name: string;
  received: Array<Record<string, unknown>>;
  server: net.Server;
  sockets: net.Socket[];
  onMessage?: (msg: Record<string, unknown>, sock: net.Socket) => void;
  waitFor(pred: (m: Record<string, unknown>) => boolean, ms?: number): Promise<Record<string, unknown>>;
  close(): Promise<void>;
}

let counter = 0;
async function startFakePlugin(): Promise<FakePlugin> {
  const name = `claudecad-test-${process.pid}-${++counter}`;
  const p = pipePath(name);
  if (process.platform !== 'win32' && fs.existsSync(p)) fs.unlinkSync(p);
  const fake: FakePlugin = {
    name,
    received: [],
    sockets: [],
    server: net.createServer(),
    async waitFor(pred, ms = 3000) {
      const t0 = Date.now();
      while (Date.now() - t0 < ms) {
        const hit = fake.received.find(pred);
        if (hit) return hit;
        await new Promise((r) => setTimeout(r, 10));
      }
      throw new Error('timeout waiting for message; got ' + JSON.stringify(fake.received));
    },
    close() {
      for (const s of fake.sockets) s.destroy();
      return new Promise((r) => fake.server.close(() => r()));
    },
  };
  fake.server.on('connection', (sock) => {
    fake.sockets.push(sock);
    const dec = new LineDecoder();
    sock.on('data', (chunk) => {
      for (const line of dec.push(chunk)) {
        const msg = JSON.parse(line) as Record<string, unknown>;
        fake.received.push(msg);
        fake.onMessage?.(msg, sock);
      }
    });
    sock.on('error', () => {});
  });
  await new Promise<void>((r) => fake.server.listen(p, () => r()));
  return fake;
}

describe('IPC over a real pipe', () => {
  const cleanups: Array<() => Promise<void>> = [];
  after(async () => {
    for (const c of cleanups) await c();
  });

  it('round-trips a tool call through the MCP tool body (echo)', async () => {
    const fake = await startFakePlugin();
    cleanups.push(() => fake.close());
    // the fake plugin answers ping by echoing the params
    fake.onMessage = (msg, sock) => {
      if (msg.type === 'tool_call') {
        sock.write(encodeMessage({ id: msg.id, ok: true, result: { pong: true, echo: (msg.params as any).message } }));
      }
    };

    const conn = await connectPipe(fake.name, 20, 50);
    const rpc = new ToolRpc((m) => conn.send(m), 2000);
    conn.on('message', (m) => {
      if ('ok' in m) rpc.handleResponse(m);
    });
    const statuses: string[] = [];
    const bridge: ToolBridge = {
      call: (t, p) => rpc.call(t, p),
      status: (t, s) => {
        statuses.push(`${t}:${s}`);
        conn.send({ type: 'tool_status', tool: t, state: s });
      },
    };
    const ping = buildTools(bridge).find((t) => t.name === 'ping')!;
    const result = await ping.handler({ message: 'xin chào' }, {});
    assert.equal(result.isError, undefined);
    assert.deepEqual(JSON.parse((result.content[0] as any).text), { pong: true, echo: 'xin chào' });
    assert.deepEqual(statuses, ['ping:running', 'ping:done']);

    const call = await fake.waitFor((m) => m.type === 'tool_call');
    assert.deepEqual(call, { id: 'req-1', type: 'tool_call', tool: 'ping', params: { message: 'xin chào' } });
    await fake.waitFor((m) => m.type === 'tool_status' && m.state === 'done');
    conn.close();
  });

  it('reports plugin errors to Claude as isError results', async () => {
    const fake = await startFakePlugin();
    cleanups.push(() => fake.close());
    fake.onMessage = (msg, sock) => {
      if (msg.type === 'tool_call') {
        sock.write(encodeMessage({ id: msg.id, ok: false, error: { code: 'ACAD_ERROR', message: 'No drawing is open' } }));
      }
    };
    const conn = await connectPipe(fake.name, 20, 50);
    const rpc = new ToolRpc((m) => conn.send(m), 2000);
    conn.on('message', (m) => {
      if ('ok' in m) rpc.handleResponse(m);
    });
    const ping = buildTools({ call: (t, p) => rpc.call(t, p), status: () => {} }).find((t) => t.name === 'ping')!;
    const result = await ping.handler({}, {});
    assert.equal(result.isError, true);
    assert.equal((result.content[0] as any).text, 'ERROR ACAD_ERROR: No drawing is open');
    conn.close();
  });

  it('times out when the plugin never answers', async () => {
    const fake = await startFakePlugin();
    cleanups.push(() => fake.close());
    const conn = await connectPipe(fake.name, 20, 50);
    const rpc = new ToolRpc((m) => conn.send(m), 100);
    const r = await rpc.call('draw_line', { start: [0, 0], end: [1, 1] });
    assert.equal(!r.ok && r.error.code, 'TIMEOUT');
    conn.close();
  });

  it('emits close and fails pending calls when the plugin drops the pipe', async () => {
    const fake = await startFakePlugin();
    cleanups.push(() => fake.close());
    const conn = await connectPipe(fake.name, 20, 50);
    const rpc = new ToolRpc((m) => conn.send(m), 5000);
    const closed = new Promise<string>((r) => conn.on('close', r));
    conn.on('close', () => rpc.failAll('ACAD_ERROR', 'Connection to AutoCAD lost.'));
    const pending = rpc.call('draw_line', {});
    await fake.waitFor((m) => m.type === 'tool_call');
    for (const s of fake.sockets) s.destroy();
    await closed;
    const r = await pending;
    assert.equal(!r.ok && r.error.message, 'Connection to AutoCAD lost.');
    assert.equal(conn.send({ type: 'turn_end' }), false);
  });

  it('drops malformed lines without breaking the stream', async () => {
    const fake = await startFakePlugin();
    cleanups.push(() => fake.close());
    const conn = await connectPipe(fake.name, 20, 50);
    const invalid: string[] = [];
    const messages: unknown[] = [];
    conn.on('invalid', (_e, line) => invalid.push(line));
    conn.on('message', (m) => messages.push(m));
    await new Promise((r) => setTimeout(r, 30));
    fake.sockets[0]!.write('not json\n{"type":"bogus"}\n{"type":"cancel"}\n');
    const t0 = Date.now();
    while (messages.length < 1 && Date.now() - t0 < 2000) await new Promise((r) => setTimeout(r, 10));
    assert.deepEqual(messages, [{ type: 'cancel' }]);
    assert.deepEqual(invalid, ['not json', '{"type":"bogus"}']);
    conn.close();
  });

  it('sidecar process connects, says ready, and exits when the pipe closes', async () => {
    const fake = await startFakePlugin();
    cleanups.push(() => fake.close());
    const entry = path.join(here, '..', 'src', 'index.js');
    const logDir = fs.mkdtempSync(path.join(path.dirname(here), '..', 'tmp-log-'));
    cleanups.push(async () => fs.rmSync(logDir, { recursive: true, force: true }));
    const child = spawn(process.execPath, [entry, '--pipe', fake.name, '--log-dir', logDir, '--work-dir', logDir], {
      stdio: 'ignore',
    });
    const exited = new Promise<number | null>((r) => child.on('exit', (code) => r(code)));
    const ready = await fake.waitFor((m) => m.type === 'ready', 10_000);
    assert.equal(typeof ready.version, 'string');
    for (const s of fake.sockets) s.destroy();
    const code = await Promise.race([exited, new Promise<string>((r) => setTimeout(() => r('timeout'), 5000))]);
    assert.equal(code, 0);
  });
});
