import assert from 'node:assert/strict';
import { test } from 'node:test';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

async function loadModule() {
  return import('../src/formats/ueextractor-csv.mjs').catch(() => ({}));
}

test('repairs an unquoted newline in a locres key when the hash sidecar confirms it', async () => {
  const { normalizeUEExtractorCsv } = await loadModule();
  assert.equal(typeof normalizeUEExtractorCsv, 'function');
  const csv = [
    'key,source,Translation',
    'Status::Provider: Git',
    'EnabledLabel,\u7b80\u4f53 {Name},',
    'Normal,\u7b80\u4f53,',
    '',
  ].join('\n');

  const result = normalizeUEExtractorCsv(csv, new Set([
    'Status::Provider: Git\nEnabledLabel',
    'Normal',
  ]));

  assert.equal(result.repairs, 1);
  assert.equal(result.text, [
    'key,source,Translation',
    '"Status::Provider: Git',
    'EnabledLabel",\u7b80\u4f53 {Name},',
    'Normal,\u7b80\u4f53,',
    '',
  ].join('\n'));
});

test('does not merge an orphan row unless the sidecar contains the combined key', async () => {
  const { normalizeUEExtractorCsv } = await loadModule();
  const csv = 'key,source,Translation\nUnexpected\nNext,Text,\n';

  const result = normalizeUEExtractorCsv(csv, new Set(['Next']));

  assert.equal(result.repairs, 0);
  assert.equal(result.text, csv);
});

test('normalizes an extracted CSV file using its BOM-prefixed hash sidecar', async () => {
  const { normalizeExtractedCsvFile } = await loadModule();
  assert.equal(typeof normalizeExtractedCsvFile, 'function');
  const directory = mkdtempSync(join(tmpdir(), 'game-translate-ueextractor-'));
  const csvPath = join(directory, 'Engine.csv');
  const hashesPath = join(directory, 'Engine.locreshashes');
  writeFileSync(csvPath, 'key,source,Translation\nParent\nChild,\u7b80\u4f53,\n', 'utf8');
  writeFileSync(hashesPath, `\uFEFF${JSON.stringify({ 'Parent\nChild': [1, 2] })}`, 'utf8');

  try {
    assert.equal(normalizeExtractedCsvFile(csvPath, hashesPath), 1);
    assert.equal(readFileSync(csvPath, 'utf8'), 'key,source,Translation\n"Parent\nChild",\u7b80\u4f53,\n');
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test('repairs an orphan key row after a converter padded its empty columns', async () => {
  const { normalizeUEExtractorCsv } = await loadModule();
  const csv = 'key,source,Translation\nParent,,\nChild,\u7b80\u4f53,\u7e41\u9ad4\n';

  const result = normalizeUEExtractorCsv(csv, new Set(['Parent\nChild']));

  assert.equal(result.repairs, 1);
  assert.equal(result.text, 'key,source,Translation\n"Parent\nChild",\u7b80\u4f53,\u7e41\u9ad4\n');
});

test('promotes the configured base-archive extraction when an active patch creates duplicates', async () => {
  const { promoteArchiveExtraction } = await loadModule();
  assert.equal(typeof promoteArchiveExtraction, 'function');
  const directory = mkdtempSync(join(tmpdir(), 'game-translate-promote-'));
  const variantCsv = join(directory, 'Game_pakchunk0-Windows.csv');
  const variantHashes = join(directory, 'Game_pakchunk0-Windows.locreshashes');
  writeFileSync(variantCsv, 'base archive CSV', 'utf8');
  writeFileSync(variantHashes, '{}', 'utf8');

  try {
    assert.equal(promoteArchiveExtraction({
      sourceDirectory: directory,
      targetName: 'Game',
      sourceArchive: 'pakchunk0-Windows.pak',
    }), true);
    assert.equal(readFileSync(join(directory, 'Game.csv'), 'utf8'), 'base archive CSV');
    assert.equal(readFileSync(join(directory, 'Game.locreshashes'), 'utf8'), '{}');
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test('promotes a base-archive CSV without hashes and removes a stale generic sidecar', async () => {
  const { promoteArchiveExtraction } = await loadModule();
  const directory = mkdtempSync(join(tmpdir(), 'game-translate-promote-no-hash-'));
  writeFileSync(join(directory, 'OnlineSubsystem_pakchunk0-Windows.csv'), 'base online CSV', 'utf8');
  writeFileSync(join(directory, 'OnlineSubsystem.locreshashes'), 'stale patch hashes', 'utf8');

  try {
    assert.equal(promoteArchiveExtraction({
      sourceDirectory: directory,
      targetName: 'OnlineSubsystem',
      sourceArchive: 'pakchunk0-Windows.pak',
    }), true);
    assert.equal(readFileSync(join(directory, 'OnlineSubsystem.csv'), 'utf8'), 'base online CSV');
    assert.equal(existsSync(join(directory, 'OnlineSubsystem.locreshashes')), false);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});
