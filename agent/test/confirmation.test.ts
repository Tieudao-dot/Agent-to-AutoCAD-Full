import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { affectedCount, confirmationError, isConfirmation, type GateState } from '../src/agent/confirmation.js';

const first = (userText: string): GateState => ({ userText, answersQuestion: false, alreadyAffected: 0 });
const answer = (userText: string, alreadyAffected = 0): GateState => ({ userText, answersQuestion: true, alreadyAffected });

const many = (n: number) => Array.from({ length: n }, (_, i) => (i + 1).toString(16).toUpperCase());

describe('confirmation gate', () => {
  it('counts affected entities, including inside batch_execute', () => {
    assert.equal(affectedCount('erase_entities', { handles: many(30) }), 30);
    assert.equal(affectedCount('draw_line', { start: [0, 0], end: [1, 1] }), 0);
    assert.equal(affectedCount('mirror_entities', { handles: many(30) }), 0); // copies by default
    assert.equal(affectedCount('mirror_entities', { handles: many(30), keep_original: false }), 30);
    assert.equal(
      affectedCount('batch_execute', {
        calls: [
          ...Array.from({ length: 25 }, () => ({ tool: 'edit_text', params: { handle: 'A', text: 'x' } })),
          { tool: 'draw_line', params: {} },
        ],
      }),
      25,
    );
  });

  it('recognises Vietnamese confirmations and refusals', () => {
    for (const yes of ['đồng ý', 'Đồng ý nhé', 'ok', 'OK xóa đi', 'có', 'Có, làm đi', 'xác nhận', 'được', 'ừ']) assert.equal(isConfirmation(yes), true, yes);
    for (const no of ['không', 'Không, đừng xóa', 'xóa toàn bộ đối tượng trên layer A-TEMP và vẽ lại toàn bộ khung tên theo mẫu mới cho tớ nhé',
      "Tìm tất cả text chứa 'WC' và đổi thành 'VỆ SINH'", 'có thể xóa hết layer A-TEMP không', 'có bao nhiêu đối tượng', undefined, '']) {
      assert.equal(isConfirmation(no), false, String(no));
    }
  });

  it('blocks a large destructive call in the first message, allows it after confirmation', () => {
    const params = { handles: many(57) };
    const err = confirmationError('erase_entities', params, first('Xóa toàn bộ đối tượng trên layer A-TEMP giúp tớ, kiểm tra kỹ trước khi làm vì bản vẽ này quan trọng'));
    assert.match(err ?? '', /Confirmation required: .*57/);
    assert.equal(confirmationError('erase_entities', params, answer('đồng ý')), undefined);
  });

  it('never blocks small edits or pure drawing', () => {
    assert.equal(confirmationError('erase_entities', { handles: many(20) }, first('xóa 20 đường này')), undefined);
    assert.equal(confirmationError('batch_execute', { calls: Array.from({ length: 200 }, () => ({ tool: 'draw_line', params: {} })) }, first('vẽ 200 đường')), undefined);
  });

  it('a first request that merely contains "xóa đi" / "sửa đi" / "tiếp tục" is not a confirmation', () => {
    const params = { handles: many(40) };
    for (const req of ['vẽ lại rồi xóa đi các đường cũ', 'tiếp tục, sửa đi toàn bộ text trên layer A-TEXT sang cao 250', 'tiếp tục vẽ các phòng còn lại']) {
      assert.match(confirmationError('erase_entities', params, first(req)) ?? '', /Confirmation required/, req);
    }
    // the phrase deep inside a sentence is not an answer even after a question
    assert.equal(isConfirmation('vẽ lại rồi xóa đi các đường cũ'), false);
    assert.equal(isConfirmation('thôi được rồi làm đi'), true);
    assert.equal(isConfirmation('ừ xóa đi'), true);
    // a short yes only counts as an answer to a question of the previous turn
    assert.match(confirmationError('erase_entities', params, first('ok')) ?? '', /Confirmation required/);
    assert.equal(confirmationError('erase_entities', params, answer('ok')), undefined);
    // the "Đồng ý" button of a card
    assert.equal(confirmationError('erase_entities', params, { userText: 'x', answersQuestion: false, confirmedByButton: true, alreadyAffected: 0 }), undefined);
  });

  it('counts over the whole turn: chunks of 20 do not slip through', () => {
    const chunk = { handles: many(20) };
    assert.equal(confirmationError('erase_entities', chunk, first('dọn layer A-TEMP')), undefined);
    const err = confirmationError('erase_entities', chunk, { ...first('dọn layer A-TEMP'), alreadyAffected: 20 });
    assert.match(err ?? '', /40 \(20 already \+ 20 now/);
    assert.match(err ?? '', /do not split/);
    assert.equal(confirmationError('erase_entities', chunk, answer('đồng ý', 80)), undefined);
    // drawing new objects never counts
    assert.equal(confirmationError('draw_line', {}, { ...first('vẽ'), alreadyAffected: 500 }), undefined);
    assert.equal(affectedCount('reset_dynamic_block', { handles: many(30) }), 30);
  });
});
