import assert from 'node:assert/strict';
import { test } from 'node:test';

async function loadModule() {
  return import('../src/core/protected-text.mjs').catch(() => ({}));
}

test('converts visible Chinese while preserving game markup byte-for-byte', async () => {
  const { convertProtectedText } = await loadModule();
  const calls = [];
  const converter = async (text) => {
    calls.push(text);
    return text.replace('你好', '你好').replace('加载', '載入');
  };

  assert.equal(typeof convertProtectedText, 'function');
  const result = await convertProtectedText(
    '你好，{PlayerName}！<RichText.Bold>加载 %1$s</>\\n[icon=sword]',
    converter,
  );

  assert.equal(result, '你好，{PlayerName}！<RichText.Bold>載入 %1$s</>\\n[icon=sword]');
  assert.deepEqual(calls, ['你好，', '加载 ']);
});

test('does not call the provider for text without CJK characters', async () => {
  const { convertProtectedText } = await loadModule();
  let calls = 0;

  assert.equal(typeof convertProtectedText, 'function');
  const result = await convertProtectedText('Press {Key} to continue...', async () => {
    calls += 1;
    return 'unexpected';
  });

  assert.equal(result, 'Press {Key} to continue...');
  assert.equal(calls, 0);
});
