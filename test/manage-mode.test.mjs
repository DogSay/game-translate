import assert from 'node:assert/strict';
import { afterEach, test } from 'node:test';
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const temporaryDirectories = [];
afterEach(() => {
  for (const directory of temporaryDirectories.splice(0)) {
    rmSync(directory, { recursive: true, force: true });
  }
});

async function loadModule() {
  return import('../src/workflows/manage-mode.mjs').catch(() => ({}));
}

function fixture() {
  const root = mkdtempSync(join(tmpdir(), 'game-translate-mode-'));
  temporaryDirectories.push(root);
  const gamePath = join(root, 'game');
  const distDir = join(root, 'dist');
  const workDir = join(root, 'work');
  const localAppDataRoot = join(root, 'local');
  const paksRelativePath = 'Sample/Content/Paks';
  const userConfigRelativePath = 'Sample/Saved/Config/Windows/GameUserSettings.ini';
  const game = {
    gamePath,
    distDir,
    workDir,
    paksRelativePath,
    sourceCulture: 'zh-Hans',
    pakVersion: 'V11',
    pakPathHashSeed: '0xD8195615',
    localizationTargets: [{
      name: 'Game',
      virtualPath: 'Sample/Content/Localization/Game/zh-Hans/Game.locres',
    }],
    userConfigRelativePath,
    defaultMode: 'compat',
    modes: {
      compat: { targetCulture: 'zh-Hans', patchName: 'sample-ZhTW-Compat_P.pak' },
      native: { targetCulture: 'zh-Hant', patchName: 'sample-ZhTW-Native_P.pak' },
    },
  };

  const paks = join(gamePath, ...paksRelativePath.split('/'));
  const configPath = join(localAppDataRoot, ...userConfigRelativePath.split('/'));
  mkdirSync(join(distDir, 'compat'), { recursive: true });
  mkdirSync(join(distDir, 'native'), { recursive: true });
  mkdirSync(paks, { recursive: true });
  mkdirSync(join(configPath, '..'), { recursive: true });
  writeFileSync(join(distDir, 'compat', game.modes.compat.patchName), 'compat artifact');
  writeFileSync(join(distDir, 'native', game.modes.native.patchName), 'native artifact');
  for (const [modeName, mode] of Object.entries(game.modes)) {
    const contents = `${modeName} artifact`;
    const sha256 = createHash('sha256').update(contents).digest('hex').toUpperCase();
    const virtualPath = game.localizationTargets[0].virtualPath.replace('zh-Hans', mode.targetCulture);
    writeFileSync(join(distDir, modeName, 'verification.json'), JSON.stringify({
      version: 1,
      mode: modeName,
      patchName: mode.patchName,
      sha256,
      targetCulture: mode.targetCulture,
      virtualPaths: [virtualPath],
      pak: { version: 'V11', pathHashSeed: 'D8195615', fileCount: 1 },
    }));
  }
  writeFileSync(join(paks, game.modes.compat.patchName), 'old compat');
  writeFileSync(
    configPath,
    ';METADATA=(Diff=true)\r\n[Internationalization]\r\nLanguage=zh-Hans\r\nLocale=zh-Hans\r\n\r\n[Other]\r\nValue=Keep\r\n',
  );
  return { game, localAppDataRoot, paks, configPath };
}

test('activates native mode externally while disabling the compatibility override', async () => {
  const { game, localAppDataRoot, paks, configPath } = fixture();
  const { activateLocalizationMode } = await loadModule();
  assert.equal(typeof activateLocalizationMode, 'function');

  const result = activateLocalizationMode({ game, modeName: 'native', localAppDataRoot, timestamp: 1234 });

  assert.equal(readFileSync(join(paks, game.modes.native.patchName), 'utf8'), 'native artifact');
  assert.equal(existsSync(join(paks, game.modes.compat.patchName)), false);
  assert.equal(existsSync(join(paks, `${game.modes.compat.patchName}.disabled`)), true);
  assert.match(readFileSync(configPath, 'utf8'), /Language=zh-Hant\r\nLocale=zh-Hant/);
  assert.match(readFileSync(configPath, 'utf8'), /\[Other\]\r\nValue=Keep/);
  assert.equal(existsSync(result.configBackup), true);
});

test('switches back to compatibility mode and restores the Simplified culture selector', async () => {
  const { game, localAppDataRoot, paks, configPath } = fixture();
  const { activateLocalizationMode } = await loadModule();

  activateLocalizationMode({ game, modeName: 'native', localAppDataRoot, timestamp: 1234 });
  activateLocalizationMode({ game, modeName: 'compat', localAppDataRoot, timestamp: 5678 });

  assert.equal(readFileSync(join(paks, game.modes.compat.patchName), 'utf8'), 'compat artifact');
  assert.equal(existsSync(join(paks, game.modes.native.patchName)), false);
  assert.match(readFileSync(configPath, 'utf8'), /Language=zh-Hans\r\nLocale=zh-Hans/);
});

test('disables every external mode and returns the selector to the source culture', async () => {
  const { game, localAppDataRoot, paks, configPath } = fixture();
  const { deactivateLocalizationModes } = await loadModule();
  assert.equal(typeof deactivateLocalizationModes, 'function');

  deactivateLocalizationModes({ game, localAppDataRoot, timestamp: 1234 });

  assert.equal(existsSync(join(paks, game.modes.compat.patchName)), false);
  assert.equal(existsSync(join(paks, `${game.modes.compat.patchName}.disabled`)), true);
  assert.match(readFileSync(configPath, 'utf8'), /Language=zh-Hans\r\nLocale=zh-Hans/);
});

test('refuses a writable pak path that escapes the game directory', async () => {
  const { game, localAppDataRoot } = fixture();
  const { activateLocalizationMode } = await loadModule();
  game.paksRelativePath = '../outside';

  assert.throws(
    () => activateLocalizationMode({ game, modeName: 'native', localAppDataRoot, timestamp: 1234 }),
    /pak.*outside.*game/i,
  );
});

test('refuses to activate an artifact changed after its verification record was written', async () => {
  const { game, localAppDataRoot } = fixture();
  const { activateLocalizationMode } = await loadModule();
  writeFileSync(join(game.distDir, 'native', game.modes.native.patchName), 'tampered artifact');

  assert.throws(
    () => activateLocalizationMode({ game, modeName: 'native', localAppDataRoot, timestamp: 1234 }),
    /hash.*verification record/i,
  );
});

test('rolls patch state back when installing the selected patch fails', async () => {
  const { game, localAppDataRoot, paks, configPath } = fixture();
  const { activateLocalizationMode } = await loadModule();
  mkdirSync(join(paks, game.modes.native.patchName));

  assert.throws(
    () => activateLocalizationMode({ game, modeName: 'native', localAppDataRoot, timestamp: 1234 }),
  );
  assert.equal(existsSync(join(paks, game.modes.compat.patchName)), true);
  assert.equal(existsSync(join(paks, `${game.modes.compat.patchName}.disabled`)), false);
  assert.match(readFileSync(configPath, 'utf8'), /Language=zh-Hans\r\nLocale=zh-Hans/);
});

test('rolls patch and culture back when installed package validation fails', async () => {
  const { game, localAppDataRoot, paks, configPath } = fixture();
  const { activateLocalizationMode } = await loadModule();
  assert.throws(() => activateLocalizationMode({
    game, modeName: 'native', localAppDataRoot, timestamp: 1234,
    verifyInstalled: () => { throw new Error('installed repak validation failed'); },
  }), /validation failed/);
  assert.equal(existsSync(join(paks, game.modes.compat.patchName)), true);
  assert.equal(existsSync(join(paks, game.modes.native.patchName)), false);
  assert.match(readFileSync(configPath, 'utf8'), /Language=zh-Hans\r\nLocale=zh-Hans/);
});

test('prepares the culture config before changing any patch state', async () => {
  const { game, localAppDataRoot, paks, configPath } = fixture();
  const { activateLocalizationMode } = await loadModule();
  rmSync(configPath);
  mkdirSync(configPath);

  assert.throws(
    () => activateLocalizationMode({ game, modeName: 'native', localAppDataRoot, timestamp: 1234 }),
  );
  assert.equal(existsSync(join(paks, game.modes.compat.patchName)), true);
  assert.equal(existsSync(join(paks, game.modes.native.patchName)), false);
});

test('installs and disables an IoStore patch as a pak utoc ucas unit', async () => {
  const { game, localAppDataRoot, paks } = fixture();
  const { activateLocalizationMode, deactivateLocalizationModes } = await loadModule();
  const mode = game.modes.compat;
  const dist = join(game.distDir, 'compat');
  const patch = join(dist, mode.patchName);
  const stem = mode.patchName.slice(0, -4);
  const companionNames = [`${stem}.utoc`, `${stem}.ucas`];
  for (const name of companionNames) writeFileSync(join(dist, name), name);
  const recordPath = join(dist, 'verification.json');
  const record = JSON.parse(readFileSync(recordPath, 'utf8'));
  record.version = 2;
  record.companions = companionNames.map((name) => ({
    name,
    bytes: Buffer.byteLength(name),
    sha256: createHash('sha256').update(name).digest('hex').toUpperCase(),
  }));
  record.sha256 = createHash('sha256').update(readFileSync(patch)).digest('hex').toUpperCase();
  writeFileSync(recordPath, JSON.stringify(record));

  activateLocalizationMode({ game, modeName: 'compat', localAppDataRoot, timestamp: 2222 });
  for (const name of companionNames) assert.equal(readFileSync(join(paks, name), 'utf8'), name);
  const installState = JSON.parse(readFileSync(join(game.workDir, 'install-state.json'), 'utf8'));
  assert.equal(installState.version, 2);
  assert.deepEqual(installState.artifact.companions.map((item) => item.name).sort(), companionNames.sort());

  deactivateLocalizationModes({ game, localAppDataRoot, timestamp: 3333 });
  const disabledState = JSON.parse(readFileSync(join(game.workDir, 'install-state.json'), 'utf8'));
  assert.equal(disabledState.active, false);
  assert.equal(disabledState.artifact.companions.length, 2);
  for (const name of companionNames) {
    assert.equal(existsSync(join(paks, name)), false);
    assert.equal(existsSync(join(paks, `${name}.disabled`)), true);
  }
});
