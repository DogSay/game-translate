import assert from 'node:assert/strict';
import { afterEach, test } from 'node:test';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const temporaryDirectories = [];
afterEach(() => {
  for (const directory of temporaryDirectories.splice(0)) {
    rmSync(directory, { recursive: true, force: true });
  }
});

test('batch converts only visible CJK segments and restores protected tokens exactly', async () => {
  const { convertProtectedTexts } = await import('../src/core/protected-text.mjs').catch(() => ({}));
  assert.equal(typeof convertProtectedTexts, 'function');
  const calls = [];
  const result = await convertProtectedTexts([
    '\u7b80\u4f53<b>{Player}</b>\u7b80\u4f53 %1$s',
    'English {Key}',
  ], async (texts) => {
    calls.push(texts);
    return texts.map((text) => text.replace('\u7b80\u4f53', '\u7e41\u9ad4'));
  });

  assert.deepEqual(result, [
    '\u7e41\u9ad4<b>{Player}</b>\u7e41\u9ad4 %1$s',
    'English {Key}',
  ]);
  assert.deepEqual(calls, [['\u7b80\u4f53', '\u7b80\u4f53 ']]);
});

test('zhconvert batch sends a JSON array in one request and validates its shape', async () => {
  const { ZhConvertClient } = await import('../src/providers/zhconvert.mjs').catch(() => ({}));
  let submittedText;
  const client = new ZhConvertClient({
    fetchImpl: async (_url, options) => {
      submittedText = new URLSearchParams(options.body).get('text');
      return {
        ok: true,
        json: async () => ({
          code: 0,
          data: { text: JSON.stringify(['\u7e41\u9ad4\u4e00', '\u7e41\u9ad4\u4e8c']) },
        }),
      };
    },
  });

  assert.deepEqual(
    await client.convertBatch(['\u7b80\u4f53\u4e00', '\u7b80\u4f53\u4e8c']),
    ['\u7e41\u9ad4\u4e00', '\u7e41\u9ad4\u4e8c'],
  );
  assert.deepEqual(JSON.parse(submittedText), ['\u7b80\u4f53\u4e00', '\u7b80\u4f53\u4e8c']);
});

test('translation memory batch sends only uncached unique text and persists each result', async () => {
  const { TranslationMemory } = await import('../src/core/translation-memory.mjs').catch(() => ({}));
  const directory = mkdtempSync(join(tmpdir(), 'game-translate-batch-memory-'));
  temporaryDirectories.push(directory);
  const path = join(directory, 'memory.json');
  const memory = new TranslationMemory(path);
  await memory.convert('\u5df2\u6709', async () => '\u5df2\u6709\u7e41');
  let providerInput;

  const result = await memory.convertBatch(
    ['\u5df2\u6709', '\u65b0\u6587', '\u65b0\u6587'],
    async (texts) => {
      providerInput = texts;
      return texts.map((text) => `${text}\u7e41`);
    },
  );

  assert.deepEqual(providerInput, ['\u65b0\u6587']);
  assert.deepEqual(result, ['\u5df2\u6709\u7e41', '\u65b0\u6587\u7e41', '\u65b0\u6587\u7e41']);
  assert.equal(new TranslationMemory(path).get('\u65b0\u6587'), '\u65b0\u6587\u7e41');
});

test('localization CSV converts blank rows in bounded batches and preserves manual translations', async () => {
  const { convertLocalizationCsv } = await import('../src/formats/localization-csv.mjs').catch(() => ({}));
  const source = [
    'key,source,Translation',
    'A,\u7b80\u4f53\u4e00,',
    'B,\u7b80\u4f53\u4e8c,\u624b\u52d5',
    'C,\u7b80\u4f53\u4e09,',
    '',
  ].join('\n');
  const batches = [];

  const result = await convertLocalizationCsv(source, null, {
    batchSize: 1,
    batchConverter: async (texts) => {
      batches.push(texts);
      return texts.map((text) => text.replace('\u7b80\u4f53', '\u7e41\u9ad4'));
    },
  });

  assert.deepEqual(batches, [['\u7b80\u4f53\u4e00'], ['\u7b80\u4f53\u4e09']]);
  assert.equal(result.text, [
    'key,source,Translation',
    'A,\u7b80\u4f53\u4e00,\u7e41\u9ad4\u4e00',
    'B,\u7b80\u4f53\u4e8c,\u624b\u52d5',
    'C,\u7b80\u4f53\u4e09,\u7e41\u9ad4\u4e09',
    '',
  ].join('\n'));
});

test('CSV file workflow publishes a completed batched conversion', async () => {
  const { convertCsvFile } = await import('../src/workflows/convert-csv-file.mjs');
  const directory = mkdtempSync(join(tmpdir(), 'game-translate-batch-file-'));
  temporaryDirectories.push(directory);
  const inputPath = join(directory, 'source.csv');
  const outputPath = join(directory, 'translated.csv');
  writeFileSync(inputPath, 'key,source,Translation\nA,\u7b80\u4f53,\n', 'utf8');

  await convertCsvFile({
    inputPath,
    outputPath,
    converter: null,
    batchConverter: async (texts) => texts.map(() => '\u7e41\u9ad4'),
    batchSize: 10,
  });

  assert.equal(readFileSync(outputPath, 'utf8'), 'key,source,Translation\nA,\u7b80\u4f53,\u7e41\u9ad4\n');
});
