/**
 * Thread "Kiểm tra độc lập" (12/11/2026): tests that PROVE bugs found in the independent review
 * (docs/ban-giao/kiem-tra-doc-lap.md). They FAIL until the owning thread fixes the bug. Do not skip them: fix the code.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { confirmationError, type GateState } from '../src/agent/confirmation.js';

describe('Kiểm tra độc lập', () => {
  // red on purpose; on GitHub Actions it is marked todo (still runs, prints "not ok … # TODO") so the one CI run reaches the UI tests
  const red = process.env.GITHUB_ACTIONS === 'true' ? { todo: 'Kiểm tra độc lập: đỏ có chủ ý' } : {};
  it('KT-09: a plain "ok" in chat is not the confirmation card for changing solids from earlier turns', red, () => {
    // Claude asked something unrelated in the previous turn ("Tường cao 3 m nhé?" sets answersQuestion via the "?"),
    // the user answered "ok". The decided rule (12/11/2026): changing solids that existed before the turn always goes
    // through the confirmation card. Today the text "ok" opens every always-confirm call of the whole turn.
    const g: GateState = { userText: 'ok', answersQuestion: true, touched: new Set(), created: new Set() };
    const err = confirmationError('solid_union', { handles: ['2F4A', '2F4B'] }, g);
    assert.ok(err, 'solid_union on two older solids ran without the confirmation card after a text "ok"');
  });
});
