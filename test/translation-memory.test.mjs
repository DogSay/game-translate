import assert from 'node:assert/strict';
import { afterEach, test } from 'node:test';
import { chmodSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const temporaryDirectories = [];
afterEach(() => {
  for (const directory of temporaryDirectories.splice(0)) {
    rmSync(directory, { recursive: true, force: true });
  }
});

async function loadModule() {
  return import('../src/core/translation-memory.mjs').catch(() => ({}));
}

function memoryPath() {
  const directory = mkdtempSync(join(tmpdir(), 'game-translate-memory-'));
  temporaryDirectories.push(directory);
  return join(directory, 'memory.json');
}

test('reuses successful conversions after a new process loads the memory file', async () => {
  const path = memoryPath();
  const { TranslationMemory } = await loadModule();
  assert.equal(typeof TranslationMemory, 'function');
  const first = new TranslationMemory(path);
  assert.equal(await first.convert('加载', async () => '載入'), '載入');

  let providerCalls = 0;
  const second = new TranslationMemory(path);
  assert.equal(await second.convert('加载', async () => {
    providerCalls += 1;
    return 'unexpected';
  }), '載入');
  assert.equal(providerCalls, 0);
});

test('never caches a failed provider result', async () => {
  const path = memoryPath();
  const { TranslationMemory } = await loadModule();
  assert.equal(typeof TranslationMemory, 'function');
  const memory = new TranslationMemory(path);

  await assert.rejects(() => memory.convert('加载', async () => { throw new Error('offline'); }), /offline/);
  assert.equal(await memory.convert('加载', async () => '載入'), '載入');
});

test('serializes concurrent saves without losing either entry', async () => {
  const path = memoryPath();
  const { TranslationMemory } = await loadModule();
  assert.equal(typeof TranslationMemory, 'function');
  const memory = new TranslationMemory(path);
  await Promise.all([
    memory.convert('加载', async () => '載入'),
    memory.convert('退出', async () => '退出'),
  ]);

  const reloaded = new TranslationMemory(path);
  assert.equal(reloaded.get('加载'), '載入');
  assert.equal(reloaded.get('退出'), '退出');
});

test('appends new entries without replacing a locked or read-only snapshot', async () => {
  const path = memoryPath();
  writeFileSync(path, JSON.stringify({ version: 1, entries: { 加载: '載入' } }), 'utf8');
  chmodSync(path, 0o444);
  const { TranslationMemory } = await loadModule();
  assert.equal(typeof TranslationMemory, 'function');
  const memory = new TranslationMemory(path);

  assert.equal(await memory.convert('退出', async () => '退出'), '退出');
  const reloaded = new TranslationMemory(path);
  assert.equal(reloaded.get('加载'), '載入');
  assert.equal(reloaded.get('退出'), '退出');
});

test('normalizes both wrapped and flat translation-memory snapshots for build consumers', async () => {
  const { translationEntriesFromDocument } = await loadModule();
  assert.equal(typeof translationEntriesFromDocument, 'function');

  assert.deepEqual(
    translationEntriesFromDocument({ version: 1, entries: { 加载: '載入' } }),
    { 加载: '載入' },
  );
  assert.deepEqual(translationEntriesFromDocument({ 退出: '退出' }), { 退出: '退出' });
  assert.deepEqual(translationEntriesFromDocument({ entries: '項目' }), { entries: '項目' });
});

test('rejects unsupported, incomplete, and non-string translation-memory snapshots', async () => {
  const { translationEntriesFromDocument } = await loadModule();

  assert.throws(
    () => translationEntriesFromDocument({ version: 999, entries: { 加载: '載入' } }),
    /unsupported.*version/i,
  );
  assert.throws(() => translationEntriesFromDocument({ version: 1 }), /entries/i);
  assert.throws(() => translationEntriesFromDocument({ 加载: 42 }), /string/i);
});
