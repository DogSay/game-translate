import assert from 'node:assert/strict';
import { afterEach, test } from 'node:test';
import { mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const temporaryDirectories = [];

afterEach(() => {
  for (const directory of temporaryDirectories.splice(0)) {
    rmSync(directory, { recursive: true, force: true });
  }
});

function files() {
  const directory = mkdtempSync(join(tmpdir(), 'game-translate-atomic-'));
  temporaryDirectories.push(directory);
  return {
    directory,
    input: join(directory, 'source.csv'),
    output: join(directory, 'translated.csv'),
  };
}

async function loadModule() {
  return import('../src/workflows/convert-csv-file.mjs').catch(() => ({}));
}

test('writes the completed CSV only after every row converts successfully', async () => {
  const paths = files();
  writeFileSync(paths.input, 'key,source,Translation\nA,加载,\n', 'utf8');
  const { convertCsvFile } = await loadModule();

  assert.equal(typeof convertCsvFile, 'function');
  const stats = await convertCsvFile({
    inputPath: paths.input,
    outputPath: paths.output,
    converter: async () => '載入',
  });

  assert.equal(readFileSync(paths.output, 'utf8'), 'key,source,Translation\nA,加载,載入\n');
  assert.deepEqual(stats, { rows: 1, converted: 1, preserved: 0 });
  assert.deepEqual(readdirSync(paths.directory).sort(), ['source.csv', 'translated.csv']);
});

test('leaves an existing output untouched when any API request fails', async () => {
  const paths = files();
  writeFileSync(paths.input, 'key,source,Translation\nA,加载,\nB,退出,\n', 'utf8');
  writeFileSync(paths.output, 'previous-good-output', 'utf8');
  const { convertCsvFile } = await loadModule();

  assert.equal(typeof convertCsvFile, 'function');
  await assert.rejects(
    () => convertCsvFile({
      inputPath: paths.input,
      outputPath: paths.output,
      converter: async (text) => {
        if (text.includes('退出')) throw new Error('API unavailable');
        return '載入';
      },
    }),
    /API unavailable/,
  );

  assert.equal(readFileSync(paths.output, 'utf8'), 'previous-good-output');
  assert.deepEqual(readdirSync(paths.directory).sort(), ['source.csv', 'translated.csv']);
});
