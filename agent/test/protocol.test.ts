import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  LineDecoder,
  ProtocolError,
  encodeMessage,
  parsePluginMessage,
} from '../src/ipc/protocol.js';

describe('encodeMessage', () => {
  it('produces exactly one line terminated by \\n', () => {
    const line = encodeMessage({
      id: 'req-17',
      type: 'tool_call',
      tool: 'draw_polyline',
      params: { vertices: [[0, 0], [4000, 0], [4000, 6000], [0, 6000]], closed: true, layer: 'A-WALL' },
    });
    assert.equal(line.endsWith('\n'), true);
    assert.equal(line.indexOf('\n'), line.length - 1);
    assert.deepEqual(JSON.parse(line), {
      id: 'req-17',
      type: 'tool_call',
      tool: 'draw_polyline',
      params: { vertices: [[0, 0], [4000, 0], [4000, 6000], [0, 6000]], closed: true, layer: 'A-WALL' },
    });
  });

  it('escapes embedded newlines and keeps Vietnamese text intact', () => {
    const line = encodeMessage({ type: 'agent_text', delta: 'Dòng 1\nDòng 2 — VỆ SINH' });
    assert.equal(line.split('\n').length, 2);
    assert.equal(JSON.parse(line).delta, 'Dòng 1\nDòng 2 — VỆ SINH');
  });

  it('rejects non-serializable values', () => {
    assert.throws(() => encodeMessage(undefined));
  });
});

describe('LineDecoder', () => {
  it('splits several messages in one chunk', () => {
    const d = new LineDecoder();
    assert.deepEqual(d.push('{"a":1}\n{"b":2}\n'), ['{"a":1}', '{"b":2}']);
  });

  it('reassembles a message split across chunks', () => {
    const d = new LineDecoder();
    assert.deepEqual(d.push('{"id":"req-1","ok":tr'), []);
    assert.deepEqual(d.push('ue,"result":{}}\n'), ['{"id":"req-1","ok":true,"result":{}}']);
  });

  it('handles a UTF-8 character split between two buffers', () => {
    const d = new LineDecoder();
    const bytes = Buffer.from('{"text":"VỆ SINH"}\n', 'utf8');
    const cut = bytes.indexOf(0xbb) > 0 ? bytes.indexOf(0xbb) : 12; // split inside a multi-byte sequence
    const first = d.push(bytes.subarray(0, cut));
    const second = d.push(bytes.subarray(cut));
    assert.deepEqual([...first, ...second], ['{"text":"VỆ SINH"}']);
    assert.equal(JSON.parse(second[0]!).text, 'VỆ SINH');
  });

  it('accepts CRLF, skips blank lines and strips a leading BOM', () => {
    const d = new LineDecoder();
    assert.deepEqual(d.push('﻿{"x":1}\r\n\r\n{"y":2}\r\n'), ['{"x":1}', '{"y":2}']);
  });

  it('returns the trailing partial line on flush', () => {
    const d = new LineDecoder();
    d.push('{"x":1}\n{"y"');
    assert.deepEqual(d.flush(), ['{"y"']);
  });

  it('guards against unbounded lines', () => {
    const d = new LineDecoder(10);
    assert.throws(() => d.push('x'.repeat(50)), ProtocolError);
  });
});

describe('parsePluginMessage', () => {
  it('parses an ok response', () => {
    assert.deepEqual(parsePluginMessage({ id: 'req-17', ok: true, result: { handles: ['2F4A'] } }), {
      id: 'req-17',
      ok: true,
      result: { handles: ['2F4A'] },
    });
  });

  it('parses an error response', () => {
    const m = parsePluginMessage({
      id: 'req-18',
      ok: false,
      error: { code: 'LAYER_NOT_FOUND', message: "Layer 'A-WALL' does not exist." },
    });
    assert.deepEqual(m, { id: 'req-18', ok: false, error: { code: 'LAYER_NOT_FOUND', message: "Layer 'A-WALL' does not exist." } });
  });

  it('parses user_message, cancel and new_session', () => {
    assert.deepEqual(parsePluginMessage({ type: 'user_message', text: 'vẽ lưới trục' }), { type: 'user_message', text: 'vẽ lưới trục' });
    assert.deepEqual(parsePluginMessage({ type: 'user_message', text: 'x', drawing: 'a.dwg' }), {
      type: 'user_message',
      text: 'x',
      drawing: 'a.dwg',
    });
    assert.deepEqual(parsePluginMessage({ type: 'cancel' }), { type: 'cancel' });
    assert.deepEqual(parsePluginMessage({ type: 'new_session' }), { type: 'new_session' });
  });

  it('rejects malformed messages', () => {
    assert.throws(() => parsePluginMessage([]), ProtocolError);
    assert.throws(() => parsePluginMessage('x'), ProtocolError);
    assert.throws(() => parsePluginMessage({ type: 'bogus' }), ProtocolError);
    assert.throws(() => parsePluginMessage({ type: 'user_message' }), ProtocolError);
    assert.throws(() => parsePluginMessage({ id: 'r', ok: false, error: 'nope' }), ProtocolError);
  });
});

describe('v0.9.4: big messages and timeouts', () => {
  it('after an over-long line the rest of it is skipped, the next line is read', () => {
    const d = new LineDecoder(10);
    assert.throws(() => d.push('x'.repeat(50)), ProtocolError);
    assert.deepEqual(d.push('yyyy'), []); // still the same over-long line
    assert.deepEqual(d.push('zz\n{"a":1}\n'), ['{"a":1}']);
  });

  it('a message with several photos fits the default limit', () => {
    const d = new LineDecoder();
    const big = '{"type":"user_message","text":"x","images":["' + 'A'.repeat(20 * 1024 * 1024) + '"]}';
    assert.equal(d.push(big + '\n').length, 1);
  });

  it('slow calls get longer timeouts', async () => {
    const { timeoutFor } = await import('../src/ipc/rpc.js');
    assert.equal(timeoutFor('draw_line', {}), 30_000);
    assert.equal(timeoutFor('batch_execute', { calls: Array.from({ length: 500 }, () => ({ tool: 'draw_line' })) }), 80_000);
    assert.equal(timeoutFor('plot_pdf', {}), 120_000);
    assert.equal(timeoutFor('batch_execute', { calls: [{ tool: 'save_drawing' }] }), 120_000);
    assert.equal(timeoutFor('batch_execute', { calls: Array.from({ length: 5000 }, () => ({ tool: 'draw_line' })) }), 300_000);
  });
});

describe('v0.9.4: skills per chat in user_message', () => {
  it('keeps valid, distinct skill names only', () => {
    const m = parsePluginMessage({ type: 'user_message', text: 'x', skills: ['ve-kien-truc', 've-kien-truc', '../etc', 'Ve Mep', 42, 'khung-ten-a3'] });
    assert.deepEqual((m as { skills?: string[] }).skills, ['ve-kien-truc', 'khung-ten-a3']);
    assert.equal('skills' in parsePluginMessage({ type: 'user_message', text: 'x' }), false);
  });
});
