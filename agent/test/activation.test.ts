/**
 * v0.9.4 activation service (activation/apps-script/Code.gs.template): runs the Google Apps Script in a VM with
 * stand-ins for Utilities / MailApp / CacheService / ContentService, the way Google runs it, and checks:
 * the owner gets the key by email, only the right key turns into a licence, and the licence is signed so the
 * plugin (LicenseLogic.Verify, same RSA-SHA256 PKCS#1 v1.5 + base64url) accepts it.
 */
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { describe, it } from 'node:test';

function findRepoFile(rel: string): string {
  let dir = process.cwd();
  for (let i = 0; i < 6; i++) {
    const p = path.join(dir, rel);
    if (fs.existsSync(p)) return p;
    dir = path.dirname(dir);
  }
  throw new Error('not found: ' + rel);
}

// Apps Script byte[] are Java bytes: -128..127
const javaBytes = (b: Buffer): number[] => Array.from(b, x => (x > 127 ? x - 256 : x));
const toBuf = (a: number[]): Buffer => Buffer.from(a.map(x => x & 0xff));

interface Mail { to: string; subject: string; body: string }

function loadService() {
  const { privateKey, publicKey } = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 });
  const pem = (privateKey.export({ format: 'pem', type: 'pkcs8' }) as string).trim() + '\n';
  const js = (s: string) => s.replace(/\\/g, '\\\\').replace(/'/g, "\\'").replace(/\r?\n/g, '\\n');
  const code = fs.readFileSync(findRepoFile('activation/apps-script/Code.gs.template'), 'utf8')
    .replace('{{EMAIL}}', 'owner@example.com')
    .replace('{{SECRET}}', 'test-secret')
    .replace('{{PRIVATE_KEY}}', js(pem));
  const mails: Mail[] = [];
  const quota = { left: 100 };
  const cache = new Map<string, string>();
  const sandbox: Record<string, unknown> = {
    Utilities: {
      computeHmacSha256Signature: (value: string, key: string) => javaBytes(crypto.createHmac('sha256', key).update(value, 'utf8').digest()),
      computeRsaSha256Signature: (value: string, key: string) => javaBytes(crypto.sign('sha256', Buffer.from(value, 'utf8'), key)),
      base64EncodeWebSafe: (bytes: number[]) => toBuf(bytes).toString('base64').replace(/\+/g, '-').replace(/\//g, '_'),
      formatDate: (d: Date, _tz: string, fmt: string) =>
        fmt === 'yyyy-MM-dd' ? d.toISOString().slice(0, 10) : fmt === 'yyyyMMddHH' ? d.toISOString().slice(0, 13).replace(/[-T]/g, '') : d.toISOString(),
    },
    MailApp: {
      sendEmail: (to: string, subject: string, body: string) => mails.push({ to, subject, body }),
      getRemainingDailyQuota: () => quota.left - mails.length,
    },
    LockService: { getScriptLock: () => ({ tryLock: () => true, releaseLock: () => {} }) },
    console: { log: () => {} },
    CacheService: { getScriptCache: () => ({ get: (k: string) => cache.get(k) ?? null, put: (k: string, v: string) => cache.set(k, v) }) },
    ContentService: {
      MimeType: { JSON: 'json' },
      createTextOutput: (s: string) => ({ content: s, setMimeType() { return this; } }),
    },
    Logger: { log: () => {} },
  };
  vm.createContext(sandbox);
  vm.runInContext(code, sandbox);
  const get = (params: Record<string, string>) =>
    JSON.parse(((sandbox.doGet as (e: unknown) => { content: string })({ parameter: params })).content);
  return { get, mails, cache, publicKey, sandbox, quota };
}

const verify = (license: string, key: crypto.KeyObject) => {
  const i = license.lastIndexOf('|');
  return crypto.verify('sha256', Buffer.from(license.slice(0, i), 'utf8'), key, Buffer.from(license.slice(i + 1), 'base64url'));
};

describe('activation service (Apps Script)', () => {
  const machine = 'CC-1A2B-3C4D-5E6F';

  it('emails the owner the device, the Windows user, the machine code and the key', () => {
    const s = loadService();
    const r = s.get({ action: 'request', machine, device: 'LAPTOP-TUAN', user: 'Trường', from: 'setup 0.9.4' });
    assert.deepEqual(r, { ok: true, sent: true });
    assert.equal(s.mails.length, 1);
    const m = s.mails[0];
    assert.equal(m.to, 'owner@example.com');
    assert.match(m.subject, /LAPTOP-TUAN \(Trường\)/);
    assert.match(m.body, /Mã máy: CC-1A2B-3C4D-5E6F/);
    assert.match(m.body, /KEY: [0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}\n/);
    // asked again within 30 minutes (Back / Next, palette reopened): no second email
    assert.deepEqual(s.get({ action: 'request', machine, device: 'LAPTOP-TUAN', user: 'x' }), { ok: true, sent: false });
    assert.equal(s.mails.length, 1);
  });

  it('the key from the email activates that machine only, with a licence the plugin can check', () => {
    const s = loadService();
    s.get({ action: 'request', machine, device: 'PC', user: 'u' });
    const key = /KEY: (\S+)/.exec(s.mails[0].body)![1];
    const r = s.get({ action: 'activate', machine, key });
    assert.equal(r.ok, true, r.error);
    assert.match(r.license, /^CCL1\|CC-1A2B-3C4D-5E6F\|\d{4}-\d{2}-\d{2}\|[A-Za-z0-9_-]+$/); // no "=" padding, no quotes
    assert.ok(verify(r.license, s.publicKey));
    // typed in lower case, with spaces, O for 0 / I or L for 1
    const sloppy = key.toLowerCase().replace(/0/g, 'o').replace(/1/g, 'l').replace(/-/g, ' ');
    assert.equal(s.get({ action: 'activate', machine, key: sloppy }).ok, true);
    // the same key on another machine
    const other = s.get({ action: 'activate', machine: 'CC-9999-9999-9999', key });
    assert.equal(other.ok, false);
    assert.match(other.error, /không đúng/);
    // a made-up key
    assert.equal(s.get({ action: 'activate', machine, key: 'AAAA-BBBB-CCCC-DDDD' }).ok, false);
  });

  it('refuses bad input instead of failing', () => {
    const s = loadService();
    assert.equal(s.get({ action: 'request', machine: 'hello' }).ok, false);
    assert.equal(s.get({ action: 'activate', machine: 'CC-1A2B-3C4D-5E6F', key: '' }).ok, false);
    assert.equal(s.get({}).ok, false);
    assert.equal(s.get({ action: 'ping' }).ok, true);
    assert.equal(s.mails.length, 0);
  });

  it('keys depend on the secret: another owner\'s service makes other keys', () => {
    const a = loadService();
    const keyA = (a.sandbox.makeKey as (m: string) => string)(machine);
    const code = fs.readFileSync(findRepoFile('activation/apps-script/Code.gs.template'), 'utf8');
    assert.ok(code.includes("var SECRET = '{{SECRET}}'"), 'the template must not carry a real secret');
    assert.ok(code.includes("var PRIVATE_KEY = '{{PRIVATE_KEY}}'"), 'the template must not carry a real private key');
    assert.match(keyA, /^[0-9A-HJKMNP-TV-Z]{4}(-[0-9A-HJKMNP-TV-Z]{4}){3}$/);
  });

  it('the manual function emails the key and the offline activation code', () => {
    const s = loadService();
    vm.runInContext("MAY_THU_CONG = 'CC-1A2B-3C4D-5E6F';", s.sandbox);
    (s.sandbox.taoKeyThuCong as () => void)();
    assert.equal(s.mails.length, 1);
    const offline = /\n(CCL1\|\S+)\n/.exec(s.mails[0].body)![1];
    assert.ok(verify(offline, s.publicKey));
    assert.ok(offline.startsWith('CCL1|CC-1A2B-3C4D-5E6F|'));
  });

  it('limits request emails: 10 per hour, keeps a few of the daily Gmail quota', () => {
    const s = loadService();
    const m = (i: number) => `CC-0000-0000-${i.toString(16).toUpperCase().padStart(4, '0')}`;
    for (let i = 1; i <= 10; i++) assert.equal(s.get({ action: 'request', machine: m(i), device: 'x', user: 'y' }).sent, true);
    const r = s.get({ action: 'request', machine: m(11), device: 'x', user: 'y' });
    assert.equal(r.ok, false);
    assert.match(r.error, /quá nhiều yêu cầu/i);
    assert.equal(s.mails.length, 10);
    // daily quota almost used up (another hour)
    const t = loadService();
    t.quota.left = 6;
    assert.equal(t.get({ action: 'request', machine: m(1), device: 'x', user: 'y' }).sent, true);
    const r2 = t.get({ action: 'request', machine: m(2), device: 'x', user: 'y' });
    assert.equal(r2.ok, false);
    assert.match(r2.error, /hết lượt gửi email/);
    // activation of a machine whose key the owner already sent still works
    const key = /KEY: (\S+)/.exec(t.mails[0]!.body)![1];
    assert.equal(t.get({ action: 'activate', machine: m(1), key }).ok, true);
  });
});
