import fs from 'node:fs';
import path from 'node:path';

let logFile: string | undefined;

export function initLog(dir: string | undefined): void {
  if (!dir) return;
  try {
    fs.mkdirSync(dir, { recursive: true });
    const d = new Date();
    const stamp = `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}`;
    logFile = path.join(dir, `sidecar-${stamp}.log`);
  } catch {
    logFile = undefined;
  }
}

/** Logs to the sidecar log file (and stderr, which the plugin also captures). */
export function log(msg: string): void {
  const line = `${new Date().toISOString()} ${msg}`;
  if (logFile) {
    try {
      fs.appendFileSync(logFile, line + '\n', 'utf8');
    } catch {
      /* ignore */
    }
  } else {
    process.stderr.write(line + '\n');
  }
}

export function truncate(s: string, max = 2000): string {
  return s.length <= max ? s : `${s.slice(0, max)}...(+${s.length - max} chars)`;
}
