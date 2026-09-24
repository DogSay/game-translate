import assert from 'node:assert/strict';
import { afterEach, test } from 'node:test';
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const temporaryDirectories = [];

afterEach(() => {
  for (const directory of temporaryDirectories.splice(0)) {
    rmSync(directory, { recursive: true, force: true });
  }
});

function gameDirectory() {
  const directory = mkdtempSync(join(tmpdir(), 'game-translate-detect-'));
  temporaryDirectories.push(directory);
  return directory;
}

async function loadDetector() {
  return import('../src/core/detect-engine.mjs').catch(() => ({}));
}

test('detects an Unreal IoStore game from its packaged layout', async () => {
  const root = gameDirectory();
  mkdirSync(join(root, 'Example', 'Content', 'Paks'), { recursive: true });
  writeFileSync(join(root, 'Example', 'Content', 'Paks', 'pakchunk0-Windows.utoc'), '');
  writeFileSync(join(root, 'Example.exe'), '');

  const { detectEngine } = await loadDetector();

  assert.equal(typeof detectEngine, 'function');
  assert.deepEqual(detectEngine(root), {
    engine: 'unreal',
    packaging: 'iostore',
    project: 'Example',
  });
});

test('detects a Unity game from its player and data directory', async () => {
  const root = gameDirectory();
  mkdirSync(join(root, 'Example_Data'), { recursive: true });
  writeFileSync(join(root, 'UnityPlayer.dll'), '');
  writeFileSync(join(root, 'Example_Data', 'globalgamemanagers'), '');

  const { detectEngine } = await loadDetector();

  assert.equal(typeof detectEngine, 'function');
  assert.deepEqual(detectEngine(root), {
    engine: 'unity',
    packaging: 'assets',
    project: 'Example',
  });
});

test('returns an explicit unknown result instead of guessing', async () => {
  const root = gameDirectory();
  const { detectEngine } = await loadDetector();

  assert.equal(typeof detectEngine, 'function');
  assert.deepEqual(detectEngine(root), {
    engine: 'unknown',
    packaging: 'unknown',
    project: null,
  });
});
