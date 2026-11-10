/**
 * v0.9.4 activation service + device management (activation/apps-script/Code.gs.template): runs the Google Apps
 * Script in a VM with stand-ins for Utilities / MailApp / CacheService / LockService / ContentService /
 * SpreadsheetApp, the way Google runs it, and checks: the owner gets the key by email, every machine lands in
 * the "Thiết bị" sheet, Active in the sheet activates without a key, Disabled locks the machine, only the right
 * key turns into a licence, and licences / statuses are signed so the plugin (LicenseLogic, same RSA-SHA256
 * PKCS#1 v1.5 + base64url) accepts them.
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

/** One sheet of a fake spreadsheet: a 2D array with the few Range methods the script uses. */
class FakeSheet {
  rows: unknown[][] = [];
  getLastRow() { return this.rows.length; }
  appendRow(r: unknown[]) { this.rows.push([...r]); }
  setFrozenRows(_n: number) {}
  private cell(r: number, c: number): unknown[] {
    while (this.rows.length < r) this.rows.push([]);
    const row = this.rows[r - 1];
    while (row.length < c) row.push('');
    return row;
  }
  getRange(row: number, col: number, numRows = 1, numCols = 1) {
    const sheet = this;
    const range = {
      getValue: () => (sheet.rows[row - 1] ?? [])[col - 1] ?? '',
      getValues: () =>
        Array.from({ length: numRows }, (_, i) =>
          Array.from({ length: numCols }, (_, j) => (sheet.rows[row - 1 + i] ?? [])[col - 1 + j] ?? '')),
      setValue(v: unknown) { sheet.cell(row, col)[col - 1] = v; return range; },
      setValues(vals: unknown[][]) {
        vals.forEach((r, i) => r.forEach((v, j) => { sheet.cell(row + i, col + j)[col - 1 + j] = v; }));
        return range;
      },
      setFontWeight() { return range; },
      setDataValidation() { return range; }, // the dropdown is cosmetic; rowStatus() tolerates any text
    };
    return range;
  }
}

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
  const sheets = new Map<string, FakeSheet>();
  const spreadsheet = {
    getSheetByName: (n: string) => sheets.get(n) ?? null,
    insertSheet: (n: string) => { const s = new FakeSheet(); sheets.set(n, s); return s; },
  };
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
    SpreadsheetApp: {
      getActive: () => spreadsheet,
      getActiveSpreadsheet: () => spreadsheet,
      newDataValidation: () => {
        const b = { requireValueInList: () => b, setAllowInvalid: () => b, build: () => ({}) };
        return b;
      },
    },
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
  const sheet = () => sheets.get('Thiết bị');
  const row = (machine: string) => sheet()?.rows.find(r => r[0] === machine);
  return { get, mails, cache, publicKey, sandbox, quota, sheet, row };
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
    assert.match(m.body, /Trạng thái thành Active/); // the sheet way is in the email too
    // asked again within 30 minutes (Back / Next, palette reopened): no second email
    assert.deepEqual(s.get({ action: 'request', machine, device: 'LAPTOP-TUAN', user: 'x' }), { ok: true, sent: false });
    assert.equal(s.mails.length, 1);
  });

  it('a new machine lands in the "Thiết bị" sheet as Chờ duyệt', () => {
    const s = loadService();
    s.get({ action: 'request', machine, device: 'LAPTOP-TUAN', user: 'Trường' });
    const row = s.row(machine)!;
    assert.ok(row, 'the request must add a row');
    assert.equal(row[1], 'LAPTOP-TUAN');
    assert.equal(row[2], 'Trường');
    assert.ok(String(row[3]).length > 0, 'Ngày xin is filled');
    assert.equal(row[5], 'Chờ duyệt');
    // asking again does not duplicate the row
    s.cache.clear();
    s.get({ action: 'request', machine, device: 'LAPTOP-TUAN', user: 'Trường' });
    assert.equal(s.sheet()!.rows.filter(r => r[0] === machine).length, 1);
  });

  it('the key from the email activates that machine only, and the row becomes Active', () => {
    const s = loadService();
    s.get({ action: 'request', machine, device: 'PC', user: 'u' });
    const key = /KEY: (\S+)/.exec(s.mails[0].body)![1];
    const r = s.get({ action: 'activate', machine, key });
    assert.equal(r.ok, true, r.error);
    assert.match(r.license, /^CCL1\|CC-1A2B-3C4D-5E6F\|\d{4}-\d{2}-\d{2}\|[A-Za-z0-9_-]+$/); // no "=" padding, no quotes
    assert.ok(verify(r.license, s.publicKey));
    assert.match(r.status, /^CCS1\|CC-1A2B-3C4D-5E6F\|active\|\d{4}-\d{2}-\d{2}\|[A-Za-z0-9_-]+$/);
    assert.ok(verify(r.status, s.publicKey));
    const row = s.row(machine)!;
    assert.equal(row[5], 'Active');
    assert.ok(String(row[4]).length > 0, 'Ngày kích hoạt is filled');
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

  it('Active in the sheet activates without a key ("Kiểm tra lại"), and last-use is recorded', () => {
    const s = loadService();
    s.get({ action: 'request', machine, device: 'PC', user: 'u' });
    // not approved yet: signed "pending", no licence
    let r = s.get({ action: 'status', machine, ver: '0.9.4' });
    assert.equal(r.ok, true);
    assert.match(r.status, /^CCS1\|CC-1A2B-3C4D-5E6F\|pending\|\d{4}-\d{2}-\d{2}\|/);
    assert.ok(verify(r.status, s.publicKey));
    assert.equal(r.license, undefined);
    // the owner flips the cell (any letter case, stray spaces): approved
    const row = s.row(machine)!;
    row[5] = '  aCtIve ';
    s.cache.delete('seen:' + machine);
    r = s.get({ action: 'status', machine, ver: '0.9.4' });
    assert.match(r.status, /\|active\|/);
    assert.ok(verify(r.status, s.publicKey));
    assert.ok(verify(r.license, s.publicKey), 'an Active machine gets its licence from ?action=status');
    assert.match(r.license, /^CCL1\|CC-1A2B-3C4D-5E6F\|/);
    assert.ok(String(row[6]).length > 0, 'Lần cuối dùng is filled');
    assert.equal(row[7], '0.9.4');
  });

  it('Disabled blocks request and activate (even with the right key), and status says so', () => {
    const s = loadService();
    s.get({ action: 'request', machine, device: 'PC', user: 'u' });
    const key = /KEY: (\S+)/.exec(s.mails[0].body)![1];
    s.row(machine)![5] = 'Disabled';
    const act = s.get({ action: 'activate', machine, key });
    assert.equal(act.ok, false);
    assert.match(act.error, /tạm khóa/);
    const req = s.get({ action: 'request', machine, device: 'PC', user: 'u' });
    assert.equal(req.ok, false);
    assert.match(req.error, /tạm khóa/);
    assert.equal(s.mails.length, 1); // no begging emails from a disabled machine
    const st = s.get({ action: 'status', machine });
    assert.match(st.status, /^CCS1\|CC-1A2B-3C4D-5E6F\|disabled\|/);
    assert.ok(verify(st.status, s.publicKey));
    assert.equal(st.license, undefined);
  });

  it('at most 30 new machines per hour reach the sheet (made-up codes cannot fill it with junk)', () => {
    const s = loadService();
    const m = (i: number) => `CC-0000-0001-${i.toString(16).toUpperCase().padStart(4, '0')}`;
    for (let i = 1; i <= 30; i++) s.get({ action: 'request', machine: m(i), device: 'x', user: 'y' });
    assert.equal(s.sheet()!.rows.filter(r => String(r[0]).startsWith('CC-0000-0001-')).length, 30);
    assert.equal(s.mails.length, 10); // the email limit still applies on its own
    const r = s.get({ action: 'request', machine: m(31), device: 'x', user: 'y' });
    assert.equal(r.ok, false);
    assert.match(r.error, /quá nhiều máy mới/);
    assert.equal(s.row(m(31)), undefined);
    // a machine already in the sheet may still ask again (no new row)
    s.cache.delete('req:' + m(12));
    assert.doesNotMatch(s.get({ action: 'request', machine: m(12), device: 'x', user: 'y' }).error ?? '', /quá nhiều máy mới/);
    // the next hour the limit starts again
    for (const k of [...s.cache.keys()]) if (k.startsWith('rows:')) s.cache.set(k, '0');
    assert.doesNotMatch(s.get({ action: 'request', machine: m(31), device: 'x', user: 'y' }).error ?? '', /quá nhiều máy mới/);
    assert.ok(s.row(m(31)));
  });

  it('an Active machine asking again (licence lost, reinstall) gets no second email', () => {
    const s = loadService();
    s.get({ action: 'request', machine, device: 'PC', user: 'u' });
    s.row(machine)![5] = 'Active';
    s.cache.clear();
    const r = s.get({ action: 'request', machine, device: 'PC', user: 'u' });
    assert.equal(r.ok, true);
    assert.equal(r.sent, false);
    assert.equal(r.approved, true);
    assert.equal(s.mails.length, 1);
    assert.ok(s.get({ action: 'status', machine }).license, 'the licence comes back through ?action=status');
  });

  it('status for an unknown machine is signed "pending" and adds no row', () => {
    const s = loadService();
    const r = s.get({ action: 'status', machine: 'CC-AAAA-BBBB-CCCC' });
    assert.equal(r.ok, true);
    assert.match(r.status, /^CCS1\|CC-AAAA-BBBB-CCCC\|pending\|/);
    assert.ok(verify(r.status, s.publicKey));
    assert.equal(s.row('CC-AAAA-BBBB-CCCC'), undefined);
  });

  it('refuses bad input instead of failing', () => {
    const s = loadService();
    assert.equal(s.get({ action: 'request', machine: 'hello' }).ok, false);
    assert.equal(s.get({ action: 'activate', machine: 'CC-1A2B-3C4D-5E6F', key: '' }).ok, false);
    assert.equal(s.get({ action: 'status', machine: 'hello' }).ok, false);
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

  it('the manual function emails the key, the offline code and the no-network pass', () => {
    const s = loadService();
    vm.runInContext("MAY_THU_CONG = 'CC-1A2B-3C4D-5E6F';", s.sandbox);
    (s.sandbox.taoKeyThuCong as () => void)();
    assert.equal(s.mails.length, 1);
    const offline = /\n(CCL1\|\S+)\n/.exec(s.mails[0].body)![1];
    assert.ok(verify(offline, s.publicKey));
    assert.ok(offline.startsWith('CCL1|CC-1A2B-3C4D-5E6F|'));
    const pass = /\n(CCS1\|\S+)\n/.exec(s.mails[0].body)![1];
    assert.ok(verify(pass, s.publicKey));
    assert.ok(pass.startsWith('CCS1|CC-1A2B-3C4D-5E6F|offline|'));
    assert.equal(s.row(machine)![5], 'Active'); // the owner sees the hand-made machine in the sheet too
  });

  it('kiemTra builds the sheet and self-tests the whole loop', () => {
    const s = loadService();
    (s.sandbox.kiemTra as () => void)();
    assert.equal(s.sheet()!.rows[0][0], 'Mã máy'); // headers in place
    assert.equal(s.row('CC-1234-5678-9ABC')![5], 'Active');
    assert.equal(s.mails.length, 1);
    assert.match(s.mails[0].subject, /hoạt động/);
  });

  it('limits request emails: 10 per hour, keeps a few of the daily Gmail quota', () => {
    const s = loadService();
    const m = (i: number) => `CC-0000-0000-${i.toString(16).toUpperCase().padStart(4, '0')}`;
    for (let i = 1; i <= 10; i++) assert.equal(s.get({ action: 'request', machine: m(i), device: 'x', user: 'y' }).sent, true);
    const r = s.get({ action: 'request', machine: m(11), device: 'x', user: 'y' });
    assert.equal(r.ok, false);
    assert.match(r.error, /quá nhiều yêu cầu/i);
    assert.equal(s.mails.length, 10);
    assert.ok(s.row(m(11)), 'the machine is in the sheet even when the email was not sent');
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
