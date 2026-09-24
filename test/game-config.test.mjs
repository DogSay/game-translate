import assert from 'node:assert/strict';
import { afterEach, test } from 'node:test';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const temporaryDirectories = [];
afterEach(() => {
  for (const directory of temporaryDirectories.splice(0)) {
    rmSync(directory, { recursive: true, force: true });
  }
});

async function loadModule() {
  return import('../src/core/game-config.mjs').catch(() => ({}));
}

function projectWith(config) {
  const root = mkdtempSync(join(tmpdir(), 'game-translate-config-'));
  temporaryDirectories.push(root);
  mkdirSync(join(root, 'games', 'sample'), { recursive: true });
  writeFileSync(join(root, 'games', 'sample', 'game.json'), JSON.stringify(config), 'utf8');
  return root;
}

test('loads a game manifest and resolves project-owned working paths', async () => {
  const root = projectWith({
    id: 'sample',
    title: 'Sample',
    gamePath: 'D:/Games/Sample',
    engine: 'unreal',
    engineVersion: 'GAME_UE5_7',
    sourceCulture: 'zh-Hans',
    targetCulture: 'zh-Hans',
    localizationTargets: [{ name: 'Game', virtualPath: 'Sample/Content/Localization/Game/zh-Hans/Game.locres' }],
  });
  const { loadGameConfig } = await loadModule();

  assert.equal(typeof loadGameConfig, 'function');
  const config = loadGameConfig(root, 'sample');

  assert.equal(config.workDir, join(root, 'work', 'sample'));
  assert.equal(config.distDir, join(root, 'dist', 'sample'));
  assert.equal(config.localizationTargets[0].name, 'Game');
});

test('loads a machine-local game path without changing the public manifest', async () => {
  const root = projectWith({
    id: 'sample',
    title: 'Sample',
    gamePath: 'C:/Public/Example/Path',
    engine: 'unreal',
    localizationTargets: [{ name: 'Game', virtualPath: 'Sample/Content/Game.locres' }],
  });
  writeFileSync(
    join(root, 'games', 'sample', 'game.local.json'),
    JSON.stringify({ gamePath: 'E:/SteamLibrary/Sample' }),
    'utf8',
  );
  const { loadGameConfig } = await loadModule();

  const config = loadGameConfig(root, 'sample');

  assert.equal(config.gamePath, 'E:/SteamLibrary/Sample');
});

test('rejects machine-local overrides of portable manifest facts', async () => {
  const root = projectWith({
    id: 'sample',
    title: 'Sample',
    gamePath: 'C:/Public/Example/Path',
    engine: 'unreal',
    localizationTargets: [{ name: 'Game', virtualPath: 'Sample/Content/Game.locres' }],
  });
  writeFileSync(
    join(root, 'games', 'sample', 'game.local.json'),
    JSON.stringify({ engine: 'unity' }),
    'utf8',
  );
  const { loadGameConfig } = await loadModule();

  assert.throws(() => loadGameConfig(root, 'sample'), /game\.local\.json.*gamePath/i);
});

test('rejects traversal in Unreal virtual paths', async () => {
  const root = projectWith({
    id: 'sample',
    title: 'Sample',
    gamePath: 'D:/Games/Sample',
    engine: 'unreal',
    localizationTargets: [{ name: 'Game', virtualPath: '../../../outside.locres' }],
  });
  const { loadGameConfig } = await loadModule();

  assert.equal(typeof loadGameConfig, 'function');
  assert.throws(() => loadGameConfig(root, 'sample'), /virtualPath.*traversal/i);
});

test('loads independent compatibility and native localization modes', async () => {
  const root = projectWith({
    id: 'sample',
    title: 'Sample',
    gamePath: 'D:/Games/Sample',
    engine: 'unreal',
    sourceCulture: 'zh-Hans',
    defaultMode: 'compat',
    userConfigRelativePath: 'Sample/Saved/Config/Windows/GameUserSettings.ini',
    modes: {
      compat: { targetCulture: 'zh-Hans', patchName: 'sample-ZhTW-Compat_P.pak' },
      native: { targetCulture: 'zh-Hant', patchName: 'sample-ZhTW-Native_P.pak' },
    },
    localizationTargets: [{ name: 'Game', virtualPath: 'Sample/Content/Localization/Game/zh-Hans/Game.locres' }],
  });
  const { loadGameConfig, resolveLocalizationMode } = await loadModule();

  const config = loadGameConfig(root, 'sample');
  assert.equal(config.defaultMode, 'compat');
  assert.deepEqual(resolveLocalizationMode(config), {
    name: 'compat',
    targetCulture: 'zh-Hans',
    patchName: 'sample-ZhTW-Compat_P.pak',
    distDir: join(root, 'dist', 'sample', 'compat'),
  });
  assert.equal(resolveLocalizationMode(config, 'native').targetCulture, 'zh-Hant');
  assert.throws(() => resolveLocalizationMode(config, 'missing'), /unknown localization mode/i);
});

test('rejects localization modes that share a patch name', async () => {
  const root = projectWith({
    id: 'sample',
    title: 'Sample',
    gamePath: 'D:/Games/Sample',
    engine: 'unreal',
    sourceCulture: 'zh-Hans',
    defaultMode: 'compat',
    userConfigRelativePath: 'Sample/Saved/Config/Windows/GameUserSettings.ini',
    modes: {
      compat: { targetCulture: 'zh-Hans', patchName: 'same_P.pak' },
      native: { targetCulture: 'zh-Hant', patchName: 'same_P.pak' },
    },
    localizationTargets: [{ name: 'Game', virtualPath: 'Sample/Content/Localization/Game/zh-Hans/Game.locres' }],
  });
  const { loadGameConfig } = await loadModule();

  assert.throws(() => loadGameConfig(root, 'sample'), /patch names must be unique/i);
});

test('rejects traversal in the writable paks directory', async () => {
  const root = projectWith({
    id: 'sample',
    title: 'Sample',
    gamePath: 'D:/Games/Sample',
    engine: 'unreal',
    sourceCulture: 'zh-Hans',
    paksRelativePath: '../Outside',
    targetCulture: 'zh-Hans',
    patchName: 'sample_P.pak',
    localizationTargets: [{ name: 'Game', virtualPath: 'Sample/Content/Localization/Game/zh-Hans/Game.locres' }],
  });
  const { loadGameConfig } = await loadModule();

  assert.throws(() => loadGameConfig(root, 'sample'), /paksRelativePath.*traversal/i);
});

test('The Mound manifest includes the runtime Engine localization target', () => {
  const manifest = JSON.parse(readFileSync(new URL('../games/the-mound/game.json', import.meta.url), 'utf8'));
  const engine = manifest.localizationTargets.find((target) => target.name === 'Engine');

  assert.deepEqual(engine, {
    name: 'Engine',
    virtualPath: 'Engine/Content/Localization/Engine/zh-Hans/Engine.locres',
  });
  assert.equal(manifest.localizationTargets.length, 8);
  assert.equal(new Set(manifest.localizationTargets.map((target) => target.name)).size, 8);
  assert.equal(new Set(manifest.localizationTargets.map((target) => target.virtualPath)).size, 8);
});
