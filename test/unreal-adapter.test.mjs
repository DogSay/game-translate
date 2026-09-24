import assert from 'node:assert/strict';
import { test } from 'node:test';

async function loadModule() {
  return import('../src/adapters/unreal.mjs').catch(() => ({}));
}

test('builds a focused no-parallel extraction command for a locres target', async () => {
  const { extractionArguments } = await loadModule();
  assert.equal(typeof extractionArguments, 'function');

  assert.deepEqual(
    extractionArguments({
      gamePath: 'C:/Games/Sample',
      outputDirectory: 'C:/Project/work/sample/source',
      virtualPath: 'Sample/Content/Localization/Game/zh-Hans/Game.locres',
    }),
    [
      'C:/Games/Sample',
      'C:/Project/work/sample/source/',
      '--path=Sample/Content/Localization/Game/zh-Hans/',
      '--no-parallel',
      '--extract-locres',
    ],
  );
});

test('maps a virtual locres path into a safe staging path', async () => {
  const { stagingPath } = await loadModule();
  assert.equal(typeof stagingPath, 'function');
  assert.equal(
    stagingPath('C:/Project/dist/sample/stage', 'Sample/Content/Localization/Game/zh-Hans/Game.locres'),
    'C:\\Project\\dist\\sample\\stage\\Sample\\Content\\Localization\\Game\\zh-Hans\\Game.locres',
  );
});

test('converts a readable hex pak seed to the decimal value repak expects', async () => {
  const { pakPackArguments } = await loadModule();
  assert.equal(typeof pakPackArguments, 'function');
  assert.deepEqual(
    pakPackArguments({
      stageDirectory: 'C:/Project/dist/sample/stage',
      outputPak: 'C:/Project/dist/sample/patch.pak',
      version: 'V11',
      pathHashSeed: '0xD8195615',
    }),
    [
      'pack', '--version', 'V11', '--path-hash-seed', '3625539093',
      '--compression', 'Zlib', 'C:/Project/dist/sample/stage', 'C:/Project/dist/sample/patch.pak',
    ],
  );
});

test('maps a source localization path to an independent target culture', async () => {
  const { localizationPathForCulture } = await loadModule();
  assert.equal(typeof localizationPathForCulture, 'function');
  assert.equal(
    localizationPathForCulture(
      'Sample/Content/Localization/Game/zh-Hans/Game.locres',
      'zh-Hans',
      'zh-Hant',
    ),
    'Sample/Content/Localization/Game/zh-Hant/Game.locres',
  );
});

test('refuses to guess where a source culture belongs in a virtual path', async () => {
  const { localizationPathForCulture } = await loadModule();
  assert.throws(
    () => localizationPathForCulture('Sample/Content/Game.locres', 'zh-Hans', 'zh-Hant'),
    /source culture segment/i,
  );
});

test('normalizes an engine version and builds retoc companion arguments', async () => {
  const { normalizeRetocVersion, iostoreCompanionArguments } = await loadModule();
  assert.equal(normalizeRetocVersion('GAME_UE5_7'), 'UE5_7');
  assert.equal(normalizeRetocVersion('5.7.4'), 'UE5_7');
  assert.deepEqual(iostoreCompanionArguments({
    patchPath: 'C:/dist/patch_P.pak',
    outputUtoc: 'C:/temp/patch_P.utoc',
    engineVersion: 'GAME_UE5_7',
  }), [
    'to-zen', 'C:/dist/patch_P.pak', 'C:/temp/patch_P.utoc', '--version', 'UE5_7',
  ]);
});
