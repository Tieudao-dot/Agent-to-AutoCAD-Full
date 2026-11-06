import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { affectedCount, confirmationError, isConfirmation } from '../src/agent/confirmation.js';

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
    const err = confirmationError('erase_entities', params, 'Xóa toàn bộ đối tượng trên layer A-TEMP giúp tớ, kiểm tra kỹ trước khi làm vì bản vẽ này quan trọng');
    assert.match(err ?? '', /Confirmation required: .*57/);
    assert.equal(confirmationError('erase_entities', params, 'đồng ý'), undefined);
  });

  it('never blocks small edits or pure drawing', () => {
    assert.equal(confirmationError('erase_entities', { handles: many(20) }, 'xóa 20 đường này'), undefined);
    assert.equal(confirmationError('batch_execute', { calls: Array.from({ length: 200 }, () => ({ tool: 'draw_line', params: {} })) }, 'vẽ 200 đường'), undefined);
  });
});
