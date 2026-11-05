import fs from 'node:fs';
import path from 'node:path';

export const SKILLS_PLUGIN_NAME = 'claudecad-skills';

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
export function prepareSkillsPlugin(skillsDir: string | undefined, workDir: string): SkillsPlugin | undefined {
  if (!skillsDir || !fs.existsSync(skillsDir)) return undefined;

  const pluginDir = path.join(workDir, SKILLS_PLUGIN_NAME);
  const target = path.join(pluginDir, 'skills');
  fs.rmSync(pluginDir, { recursive: true, force: true });
  fs.mkdirSync(path.join(pluginDir, '.claude-plugin'), { recursive: true });
  fs.mkdirSync(target, { recursive: true });

  const skills: string[] = [];
  for (const entry of fs.readdirSync(skillsDir, { withFileTypes: true })) {
    if (!entry.isDirectory() || entry.name.startsWith('.') || entry.name.startsWith('_')) continue;
    const src = path.join(skillsDir, entry.name);
    if (!fs.existsSync(path.join(src, 'SKILL.md'))) continue;
    fs.cpSync(src, path.join(target, entry.name), { recursive: true });
    skills.push(entry.name);
  }

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
