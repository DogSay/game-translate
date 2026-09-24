import assert from 'node:assert/strict';
import { test } from 'node:test';

async function loadModule() {
  return import('../src/providers/zhconvert.mjs').catch(() => ({}));
}

test('sends UTF-8 text to the Taiwan converter with POST', async () => {
  const { ZhConvertClient } = await loadModule();
  let request;
  const fetchImpl = async (url, options) => {
    request = { url, options };
    return {
      ok: true,
      json: async () => ({ code: 0, data: { text: '載入遊戲' }, msg: '' }),
    };
  };

  assert.equal(typeof ZhConvertClient, 'function');
  const client = new ZhConvertClient({ fetchImpl });
  const converted = await client.convert('加载游戏');

  assert.equal(converted, '載入遊戲');
  assert.equal(request.url, 'https://api.zhconvert.org/convert');
  assert.equal(request.options.method, 'POST');
  const body = new URLSearchParams(request.options.body);
  assert.equal(body.get('converter'), 'Taiwan');
  assert.equal(body.get('text'), '加载游戏');
});

test('treats a non-zero API code as a failed conversion', async () => {
  const { ZhConvertClient } = await loadModule();
  assert.equal(typeof ZhConvertClient, 'function');
  const client = new ZhConvertClient({
    fetchImpl: async () => ({
      ok: true,
      json: async () => ({ code: 42, msg: 'conversion failed' }),
    }),
  });

  await assert.rejects(() => client.convert('加载'), /conversion failed/);
});

test('caches identical text during a run', async () => {
  const { ZhConvertClient } = await loadModule();
  let calls = 0;
  assert.equal(typeof ZhConvertClient, 'function');
  const client = new ZhConvertClient({
    fetchImpl: async () => {
      calls += 1;
      return { ok: true, json: async () => ({ code: 0, data: { text: '遊戲' } }) };
    },
  });

  assert.equal(await client.convert('游戏'), '遊戲');
  assert.equal(await client.convert('游戏'), '遊戲');
  assert.equal(calls, 1);
});

test('retries a transient HTTP failure without caching the failure', async () => {
  const { ZhConvertClient } = await loadModule();
  let calls = 0;
  assert.equal(typeof ZhConvertClient, 'function');
  const client = new ZhConvertClient({
    retryDelay: async () => {},
    fetchImpl: async () => {
      calls += 1;
      if (calls === 1) return { ok: false, status: 503 };
      return { ok: true, json: async () => ({ code: 0, data: { text: '載入' } }) };
    },
  });

  assert.equal(await client.convert('加载'), '載入');
  assert.equal(calls, 2);
});

test('honors Retry-After when the service rate limits a request', async () => {
  const { ZhConvertClient } = await loadModule();
  let calls = 0;
  const delays = [];
  const client = new ZhConvertClient({
    retryDelay: async (milliseconds) => delays.push(milliseconds),
    fetchImpl: async () => {
      calls += 1;
      if (calls === 1) {
        return { ok: false, status: 429, headers: { get: () => '2' } };
      }
      return { ok: true, json: async () => ({ code: 0, data: { text: '載入' } }) };
    },
  });

  assert.equal(await client.convert('加载'), '載入');
  assert.deepEqual(delays, [2000]);
});
