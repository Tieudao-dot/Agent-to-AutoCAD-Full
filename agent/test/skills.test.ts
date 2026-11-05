/**
 * The bundled sample skills must follow the Agent Skills format and only mention
 * tools that exist, otherwise Claude would be told to call tools it does not have.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { buildTools } from '../src/tools/index.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const skillsDir = path.resolve(here, '..', '..', '..', 'skills');
const toolNames = new Set(buildTools({ call: async () => ({ id: '', ok: true, result: {} }), status: () => {} }).map((t) => t.name));
const TOOL_PREFIX = /^(draw|add|create|list|set|get|insert|define|find|measure|count|move|copy|rotate|scale|mirror|offset|array|fillet|chamfer|explode|join|erase|edit|zoom|regen|save|purge|undo|batch)_[a-z0-9_]+$/;

function frontmatter(md: string): Record<string, string> {
  const m = /^---\r?\n([\s\S]*?)\r?\n---/.exec(md);
  assert.ok(m, 'missing YAML frontmatter');
  const out: Record<string, string> = {};
  for (const line of m[1]!.split(/\r?\n/)) {
    const kv = /^([a-z_-]+):\s*(.*)$/.exec(line);
    if (kv) out[kv[1]!] = kv[2]!.trim();
  }
  return out;
}

const skills = fs.readdirSync(skillsDir, { withFileTypes: true }).filter((d) => d.isDirectory()).map((d) => d.name);

describe('sample skills', () => {
  it('ships the three skills of spec §8', () => {
    for (const s of ['ve-kien-truc', 've-mep', 've-ha-tang']) assert.ok(skills.includes(s), s);
  });

  for (const name of skills) {
    describe(name, () => {
      const md = fs.readFileSync(path.join(skillsDir, name, 'SKILL.md'), 'utf8');

      it('has valid frontmatter (name = folder, description <= 1024 chars)', () => {
        const fm = frontmatter(md);
        assert.equal(fm.name, name);
        assert.match(fm.name!, /^[a-z0-9-]{1,64}$/);
        assert.ok(fm.description && fm.description.length >= 100 && fm.description.length <= 1024, `description length ${fm.description?.length}`);
      });

      it('has the mandatory "Quy trình chuẩn bị" section calling get_drawing_info + list_layers', () => {
        const idx = md.indexOf('Quy trình chuẩn bị');
        assert.ok(idx > 0);
        const section = md.slice(idx, idx + 1500);
        assert.match(section, /get_drawing_info/);
        assert.match(section, /list_layers/);
      });

      it('only references tools that exist', () => {
        const mentioned = new Set<string>();
        for (const m of md.matchAll(/`([a-z0-9_]+)`/g)) if (TOOL_PREFIX.test(m[1]!)) mentioned.add(m[1]!);
        for (const m of md.matchAll(/"tool":\s*"([a-z0-9_]+)"/g)) mentioned.add(m[1]!);
        const unknown = [...mentioned].filter((t) => !toolNames.has(t));
        assert.deepEqual(unknown, []);
      });

      it('JSON examples are valid JSON', () => {
        for (const m of md.matchAll(/```json\n([\s\S]*?)```/g)) assert.doesNotThrow(() => JSON.parse(m[1]!), m[1]!.slice(0, 80));
      });
    });
  }
});
