import fs from 'node:fs';
import path from 'node:path';

export const SKILLS_PLUGIN_NAME = 'claudecad-skills';

/** Names of skills turned off with the palette's Skill list (skills.json next to the skills). */
export function disabledSkills(skillsDir: string): Set<string> {
  const off = new Set<string>();
  try {
    const reg = JSON.parse(fs.readFileSync(path.join(skillsDir, 'skills.json'), 'utf8')) as { skills?: Array<{ name?: unknown; enabled?: unknown }> };
    for (const s of reg.skills ?? []) if (typeof s.name === 'string' && s.enabled === false) off.add(s.name);
  } catch {
    /* no registry yet: everything is enabled */
  }
  return off;
}

/** Every skill folder of the library (has SKILL.md, not hidden), enabled or not. */
export function librarySkills(skillsDir: string | undefined): string[] {
  if (!skillsDir || !fs.existsSync(skillsDir)) return [];
  return fs
    .readdirSync(skillsDir, { withFileTypes: true })
    .filter((e) => e.isDirectory() && !e.name.startsWith('.') && !e.name.startsWith('_'))
    .filter((e) => fs.existsSync(path.join(skillsDir, e.name, 'SKILL.md')))
    .map((e) => e.name)
    .sort();
}

/** Skill folders that will be loaded (have SKILL.md, not hidden, not disabled). */
export function enabledSkills(skillsDir: string | undefined): string[] {
  if (!skillsDir || !fs.existsSync(skillsDir)) return [];
  const off = disabledSkills(skillsDir);
  return fs
    .readdirSync(skillsDir, { withFileTypes: true })
    .filter((e) => e.isDirectory() && !e.name.startsWith('.') && !e.name.startsWith('_') && !off.has(e.name))
    .filter((e) => fs.existsSync(path.join(skillsDir, e.name, 'SKILL.md')))
    .map((e) => e.name)
    .sort();
}

/**
 * The service plan a skill needs, from its SKILL.md frontmatter: "plan: 3d" = only useful with the 3D tools (3D kiến
 * trúc), "plan: 2d" = a 2D drafting standard (text, dimensions, hatch, offset… that a 3D machine does not have: the
 * samples ve-kien-truc, ve-mep, ve-ha-tang; decided 12/11/2026). undefined = every plan (the user's own skills).
 */
export function skillPlan(skillsDir: string, name: string): '2d' | '3d' | undefined {
  try {
    const md = fs.readFileSync(path.join(skillsDir, name, 'SKILL.md'), 'utf8').replace(/^\uFEFF/, '');
    const fm = /^---\r?\n([\s\S]*?)\r?\n---/.exec(md);
    if (!fm) return undefined;
    const m = /^plan:\s*["']?([A-Za-z0-9]+)["']?\s*$/m.exec(fm[1]!);
    const v = m ? m[1]!.toLowerCase() : undefined;
    return v === '3d' ? '3d' : v === '2d' ? '2d' : undefined;
  } catch {
    return undefined;
  }
}

/**
 * Skills a machine of this plan can use: a "plan: 3d" skill is left out on a 2D machine and a "plan: 2d" skill on a 3D
 * machine, which lack those tools (the skill would only cost tokens and invite Claude to try). Full and an older plugin
 * (no plan) load every skill; skills without a plan line load everywhere.
 */
export function skillsForPlan(skillsDir: string | undefined, names: string[], plan?: string): string[] {
  if (!skillsDir || !plan || plan === '2d3d') return names;
  const other = plan === '2d' ? '3d' : plan === '3d' ? '2d' : undefined;
  if (!other) return names;
  return names.filter((n) => skillPlan(skillsDir, n) !== other);
}

export interface SkillsPlugin {
  /** Directory handed to the SDK as a local plugin. */
  pluginDir: string;
  /** Skill folder names found (each has a SKILL.md). */
  skills: string[];
}

/**
 * Builds a local Claude Code plugin from the user-editable skills folder (spec §8).
 *
 * The SDK loads skills from plugin directories (`<plugin>/skills/<name>/SKILL.md`).
 * We copy the bundle's `skills/` folder into a generated plugin under the
 * sidecar's work directory at every session start, so adding or editing a skill
 * only needs a new chat (New button) — no rebuild, no symlink privileges.
 */
export function prepareSkillsPlugin(skillsDir: string | undefined, workDir: string, only?: string[], plan?: string): SkillsPlugin | undefined {
  if (!skillsDir || !fs.existsSync(skillsDir)) return undefined;

  const pluginDir = path.join(workDir, SKILLS_PLUGIN_NAME);
  const target = path.join(pluginDir, 'skills');
  // the Claude process that used it may still hold files open for a moment (Windows: EPERM / ENOTEMPTY)
  fs.rmSync(pluginDir, { recursive: true, force: true, maxRetries: 10, retryDelay: 100 });
  fs.mkdirSync(path.join(pluginDir, '.claude-plugin'), { recursive: true });
  fs.mkdirSync(target, { recursive: true });

  // the chat's own list when the palette sends one (v0.9.4: skills per chat), else the enabled ones
  // the service plan of the machine: a 3D-only skill is not loaded on a 2D machine
  const skills = skillsForPlan(skillsDir, only ? librarySkills(skillsDir).filter((s) => only.includes(s)) : enabledSkills(skillsDir), plan);
  for (const name of skills) fs.cpSync(path.join(skillsDir, name), path.join(target, name), { recursive: true });

  fs.writeFileSync(
    path.join(pluginDir, '.claude-plugin', 'plugin.json'),
    JSON.stringify(
      {
        name: SKILLS_PLUGIN_NAME,
        version: '1.0.0',
        description: 'ClaudeCAD drafting standards (user-editable skills copied from the bundle skills/ folder).',
      },
      null,
      2,
    ),
    'utf8',
  );
  return { pluginDir, skills };
}
