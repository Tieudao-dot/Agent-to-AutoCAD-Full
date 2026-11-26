/**
 * Nâng cấp đợt H9: the language the user chose in the palette ("vi" default, "en"). The texts this assistant shows the
 * user itself (error lines, the short tool summaries) go through tr(): the Vietnamese text is the key, i18nEn.ts holds
 * its English; a text with no English stays Vietnamese (a test lists them). Claude's own answers follow the system
 * prompt's language section (systemPrompt.ts).
 */
import { EN } from './i18nEn.js';

export type Language = 'vi' | 'en';

let current: Language = 'vi';

export function setLanguage(lang: string | undefined): void {
  current = lang === 'en' ? 'en' : 'vi';
}

export function language(): Language {
  return current;
}

/** The text in the current language; {0}, {1}… are filled with args after the translation. */
export function tr(vi: string, ...args: unknown[]): string {
  const t = current === 'en' ? (EN[vi] ?? vi) : vi;
  return args.length === 0 ? t : t.replace(/\{(\d+)\}/g, (m, i: string) => (Number(i) < args.length ? String(args[Number(i)]) : m));
}

/** True when the text has an English entry (tests). */
export function hasEnglish(vi: string): boolean {
  return Object.prototype.hasOwnProperty.call(EN, vi);
}
