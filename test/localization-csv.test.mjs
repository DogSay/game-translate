import assert from 'node:assert/strict';
import { test } from 'node:test';

async function loadModule() {
  return import('../src/formats/localization-csv.mjs').catch(() => ({}));
}

test('parses quoted commas and multiline fields without losing data', async () => {
  const { parseCsv, stringifyCsv } = await loadModule();
  const source = 'key,source,Translation\r\nA,"第一行, 有逗號\r\n第二行",\r\n';

  assert.equal(typeof parseCsv, 'function');
  assert.equal(typeof stringifyCsv, 'function');
  const rows = parseCsv(source);

  assert.deepEqual(rows, [
    ['key', 'source', 'Translation'],
    ['A', '第一行, 有逗號\r\n第二行', ''],
  ]);
  assert.equal(stringifyCsv(rows, '\r\n'), source);
});

test('fills blank Translation cells and preserves manual translations', async () => {
  const { convertLocalizationCsv } = await loadModule();
  const source = [
    'key,source,Translation',
    'A,加载游戏,',
    'B,退出游戏,手動譯文',
    '',
  ].join('\n');
  const converter = async (text) => text.replace('加载游戏', '載入遊戲');

  assert.equal(typeof convertLocalizationCsv, 'function');
  const result = await convertLocalizationCsv(source, converter);

  assert.equal(result.text, [
    'key,source,Translation',
    'A,加载游戏,載入遊戲',
    'B,退出游戏,手動譯文',
    '',
  ].join('\n'));
  assert.deepEqual(result.stats, { rows: 2, converted: 1, preserved: 1 });
});

test('rejects CSV without source and Translation columns', async () => {
  const { convertLocalizationCsv } = await loadModule();
  assert.equal(typeof convertLocalizationCsv, 'function');

  await assert.rejects(
    () => convertLocalizationCsv('id,text\n1,加载\n', async (text) => text),
    /source.*Translation/i,
  );
});

test('converts independent rows with bounded concurrency while preserving row order', async () => {
  const { convertLocalizationCsv } = await loadModule();
  const source = 'key,source,Translation\nA,加载一,\nB,加载二,\nC,加载三,\n';
  let active = 0;
  let maximumActive = 0;
  const converter = async (text) => {
    active += 1;
    maximumActive = Math.max(maximumActive, active);
    await new Promise((resolve) => setTimeout(resolve, 5));
    active -= 1;
    return text.replace('加载', '載入');
  };

  const result = await convertLocalizationCsv(source, converter, { concurrency: 2 });

  assert.equal(maximumActive, 2);
  assert.equal(result.text, 'key,source,Translation\nA,加载一,載入一\nB,加载二,載入二\nC,加载三,載入三\n');
});
