#!/usr/bin/env node
/**
 * Fills activation/apps-script/Code.gs.template with the owner's email, the key secret and the RSA private key,
 * producing the Code.gs the owner pastes into script.google.com. The output is SECRET: never commit it.
 *
 *   node activation/make-apps-script.mjs --private-key D:/khoa/private.pem --email me@gmail.com --out D:/khoa/Code.gs
 *   (--out must be outside the repository: the file holds the secret and the private key; KT-39, 12/11/2026)
 *        [--secret <base64url>]     (default: a new random secret — keys made with an old secret stop working)
 *
 * The matching public key must be in plugin/ClaudeCAD.Plugin/Core/License.cs (LicenseLogic.PublicModulus):
 *   node activation/make-apps-script.mjs --private-key license-private.pem --print-public
 */
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';

const { values } = parseArgs({
  options: {
    'private-key': { type: 'string' },
    email: { type: 'string' },
    secret: { type: 'string' },
    out: { type: 'string' },
    'print-public': { type: 'boolean' },
  },
});

function fail(msg) {
  console.error('make-apps-script: ' + msg);
  process.exit(1);
}

if (!values['private-key']) fail('--private-key <pem> is required');
const pem = fs.readFileSync(values['private-key'], 'utf8');
const key = crypto.createPrivateKey(pem);
if (key.asymmetricKeyType !== 'rsa') fail('the private key must be RSA');

if (values['print-public']) {
  const jwk = crypto.createPublicKey(key).export({ format: 'jwk' });
  console.log('PublicModulus = "' + Buffer.from(jwk.n, 'base64url').toString('base64') + '"');
  console.log('PublicExponent = "' + Buffer.from(jwk.e, 'base64url').toString('base64') + '"');
  process.exit(0);
}

if (!values.email || !/^[^@\s]+@[^@\s]+$/.test(values.email)) fail('--email <address> is required');
if (!values.out) fail('--out <file> is required');

// Apps Script wants PKCS#8 ("BEGIN PRIVATE KEY")
const pkcs8 = key.export({ format: 'pem', type: 'pkcs8' }).toString().trim();
const secret = values.secret ?? crypto.randomBytes(32).toString('base64url');
const here = path.dirname(fileURLToPath(import.meta.url));
const template = fs.readFileSync(path.join(here, 'apps-script', 'Code.gs.template'), 'utf8');
const js = s => s.replace(/\\/g, '\\\\').replace(/'/g, "\\'").replace(/\r?\n/g, '\\n');
const out = template
  .replace('{{EMAIL}}', js(values.email))
  .replace('{{SECRET}}', js(secret))
  .replace('{{PRIVATE_KEY}}', js(pkcs8 + '\n'));
if (out.includes('{{')) fail('template placeholder left unfilled');
// KT-39 (12/11/2026): never inside the repository, where a "git add ." would take the secret and the private key along
const repoRoot = path.resolve(here, '..');
const rel = path.relative(repoRoot, path.resolve(values.out));
if (rel === '' || (!rel.startsWith('..') && !path.isAbsolute(rel)))
  fail(`--out must be outside the repository (${repoRoot}): the file holds the secret and the private key. Example: --out D:/claudecad-full-khoa/Code.gs`);
fs.writeFileSync(values.out, out, 'utf8');
console.log(`make-apps-script: wrote ${values.out} (secret ${values.secret ? 'given' : 'new'}). Keep it private.`);
