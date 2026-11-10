#!/usr/bin/env node
/**
 * ClaudeCAD sidecar entry point.
 *   node dist/index.js --pipe claudecad-<acadPid> [--skills <dir>] [--log-dir <dir>] [--parent-pid <pid>]
 * Spawned by the AutoCAD plugin (SidecarManager); connects to the plugin's
 * named pipe and runs the Agent SDK session.
 */
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { AgentSession } from './agent/session.js';
import { connectPipe } from './ipc/connection.js';
import { DEFAULT_TOOL_TIMEOUT_MS } from './ipc/protocol.js';
import { ToolRpc } from './ipc/rpc.js';
import { initLog, log, truncate } from './log.js';
import type { ToolBridge } from './tools/common.js';

export const VERSION = '0.9.4';

async function main(): Promise<void> {
  const { values } = parseArgs({
    options: {
      pipe: { type: 'string' },
      skills: { type: 'string' },
      'log-dir': { type: 'string' },
      'parent-pid': { type: 'string' },
      'work-dir': { type: 'string' },
    },
    allowPositionals: true,
  });

  if (!values.pipe) {
    process.stderr.write('usage: node dist/index.js --pipe <name> [--skills <dir>] [--log-dir <dir>]\n');
    process.exit(2);
  }
  initLog(values['log-dir']);
  // before anything can throw asynchronously: a stray error must not kill the sidecar ("mất kết nối")
  process.on('uncaughtException', (err) => log(`uncaughtException: ${err.stack ?? err}`));
  process.on('unhandledRejection', (err) => log(`unhandledRejection: ${(err as Error)?.stack ?? err}`));
  log(`ClaudeCAD sidecar ${VERSION} starting (node ${process.version}, pid ${process.pid})`);

  const workDir =
    values['work-dir'] ??
    path.join(process.env.LOCALAPPDATA ?? path.join(os.homedir(), '.local', 'share'), 'ClaudeCAD', 'workspace');
  fs.mkdirSync(workDir, { recursive: true });

  const conn = await connectPipe(values.pipe);
  log(`connected to pipe ${values.pipe}`);

  const timeoutMs = Number(process.env.CLAUDECAD_TOOL_TIMEOUT_MS) || DEFAULT_TOOL_TIMEOUT_MS; // base; big calls get more (rpc.ts)
  // Tool parameters and results carry drawing content (texts, coordinates, layer names): the log keeps only
  // what is needed to debug (tool, id, size, error) unless CLAUDECAD_LOG_FULL=1.
  const fullLog = process.env.CLAUDECAD_LOG_FULL === '1';
  const rpc = new ToolRpc((msg) => {
    log(fullLog ? `-> ${truncate(JSON.stringify(msg))}` : `-> ${msg.id} ${msg.tool} (${JSON.stringify(msg.params).length} chars)`);
    return conn.send(msg);
  }, timeoutMs);

  // eslint-disable-next-line prefer-const
  let session: AgentSession;
  const bridge: ToolBridge = {
    call: async (tool, params) => {
      const block = session?.gate(tool, params);
      if (block) {
        log(`confirmation gate blocked ${tool}`);
        return { id: 'local', ok: false, error: { code: 'INVALID_PARAMS', message: block } };
      }
      return rpc.call(tool, params);
    },
    status: (tool, state, extra) => conn.send({ type: 'tool_status', tool, state, ...(extra ?? {}) }),
  };

  session = new AgentSession({
    emit: (m) => conn.send(m),
    bridge,
    version: VERSION,
    workDir,
    skillsDir: values.skills,
    model: process.env.CLAUDECAD_MODEL || 'claude-opus-5-5',
    fallbackModel: process.env.CLAUDECAD_FALLBACK_MODEL || 'claude-sonnet-5-5',
    log,
    onCancel: () => rpc.failAll('ACAD_ERROR', 'Cancelled by the user (Stop). Do not continue this task.'),
  });

  conn.on('message', (msg) => {
    if ('ok' in msg) {
      log(fullLog ? `<- ${truncate(JSON.stringify(msg))}` : msg.ok ? `<- ${msg.id} ok` : `<- ${msg.id} ${msg.error.code}: ${truncate(msg.error.message, 300)}`);
      if (!rpc.handleResponse(msg)) log(`late/unknown response ${msg.id} ignored`);
      return;
    }
    log(`<- ${msg.type}`);
    switch (msg.type) {
      case 'user_message':
        session.sendMessage(msg);
        break;
      case 'set_options':
        void session.setOptions(msg);
        break;
      case 'switch_session':
        session.switchTo(msg.session_id);
        break;
      case 'reload_skills':
        session.reloadSkills();
        break;
      case 'cancel':
        void session.cancel();
        break;
      case 'new_session':
        session.reset();
        break;
    }
  });
  conn.on('invalid', (err, line) => {
    log(`invalid message dropped: ${err.message} ${truncate(line, 200)}`);
    // A user_message that could not be read (e.g. several large photos over the size limit) would leave the
    // palette waiting for a turn_end that never comes. While no turn runs, an unreadable line can only be one.
    if (!session.busy && (line === '' || line.includes('"user_message"'))) {
      conn.send({ type: 'turn_end', error: 'Trợ lý không đọc được tin nhắn (có thể do ảnh đính kèm quá lớn). Bớt ảnh hoặc dùng ảnh nhỏ hơn rồi gửi lại.' });
    }
  });
  conn.on('close', (reason) => {
    log(`pipe closed: ${reason}; exiting`);
    rpc.failAll('ACAD_ERROR', 'Connection to AutoCAD lost.');
    session.close();
    setTimeout(() => process.exit(0), 200);
  });

  conn.send({ type: 'ready', info: 'sidecar sẵn sàng', version: VERSION });

  // Exit if AutoCAD disappears without closing the pipe.
  const parentPid = Number(values['parent-pid']);
  if (parentPid > 0) {
    setInterval(() => {
      try {
        process.kill(parentPid, 0);
      } catch {
        log('parent process gone; exiting');
        process.exit(0);
      }
    }, 5000).unref();
  }

}

main().catch((err) => {
  log(`fatal: ${(err as Error).stack ?? err}`);
  process.exit(1);
});
