import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  renameSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import { dirname, isAbsolute, join, relative, resolve } from 'node:path';
import { resolveLocalizationMode } from '../core/game-config.mjs';
import { localizationPathForCulture } from '../adapters/unreal.mjs';
import { assertVerifiedArtifact, sha256File } from './verify-artifact.mjs';

function pathFromRelative(root, relativePath, label = 'relative path') {
  const rootPath = resolve(root);
  const target = resolve(rootPath, ...relativePath.replaceAll('\\', '/').split('/'));
  const contained = relative(rootPath, target);
  if (contained === '' || contained.startsWith('..') || isAbsolute(contained)) {
    throw new Error(`${label} resolves outside its allowed root: ${relativePath}`);
  }
  return target;
}

function availableDisabledPath(activePath, timestamp) {
  const preferred = `${activePath}.disabled`;
  if (!existsSync(preferred)) return preferred;
  let suffix = 0;
  let candidate;
  do {
    candidate = `${preferred}.${timestamp}${suffix === 0 ? '' : `.${suffix}`}`;
    suffix += 1;
  } while (existsSync(candidate));
  return candidate;
}

function disablePatch(activePath, timestamp) {
  if (!existsSync(activePath)) return null;
  const disabledPath = availableDisabledPath(activePath, timestamp);
  renameSync(activePath, disabledPath);
  return disabledPath;
}

function artifactNames(patchName, verification = null) {
  return [patchName, ...(verification?.companions ?? []).map((item) => item.name)];
}

function disableArtifacts(paksDirectory, names, timestamp) {
  const disabled = [];
  try {
    for (const name of names) {
      const active = join(paksDirectory, name);
      const disabledPath = disablePatch(active, timestamp);
      if (disabledPath) disabled.push({ active, disabledPath });
    }
    return disabled;
  } catch (error) {
    for (const item of disabled.reverse()) renameSync(item.disabledPath, item.active);
    throw error;
  }
}

function updateCultureIni(contents, culture) {
  const newline = contents.includes('\r\n') ? '\r\n' : '\n';
  const lines = contents.split(/\r?\n/);
  let sectionStart = lines.findIndex((line) => line.trim().toLowerCase() === '[internationalization]');
  if (sectionStart < 0) {
    if (lines.length > 0 && lines.at(-1) !== '') lines.push('');
    lines.push('[Internationalization]', `Language=${culture}`, `Locale=${culture}`);
    return lines.join(newline);
  }

  let sectionEnd = lines.findIndex((line, index) => index > sectionStart && /^\s*\[[^\]]+\]\s*$/.test(line));
  if (sectionEnd < 0) sectionEnd = lines.length;
  for (const key of ['Language', 'Locale']) {
    const keyIndex = lines.findIndex((line, index) => (
      index > sectionStart
      && index < sectionEnd
      && new RegExp(`^\\s*${key}\\s*=`, 'i').test(line)
    ));
    if (keyIndex >= 0) {
      lines[keyIndex] = `${key}=${culture}`;
    } else {
      lines.splice(sectionEnd, 0, `${key}=${culture}`);
      sectionEnd += 1;
    }
  }
  return lines.join(newline);
}

function availablePath(preferred) {
  if (!existsSync(preferred)) return preferred;
  let suffix = 1;
  while (existsSync(`${preferred}.${suffix}`)) suffix += 1;
  return `${preferred}.${suffix}`;
}

function prepareCulture(game, localAppDataRoot, culture, timestamp) {
  if (!game.userConfigRelativePath) {
    throw new Error('userConfigRelativePath is required to activate a localization mode');
  }
  if (!localAppDataRoot) throw new Error('LOCALAPPDATA is required to activate a localization mode');
  const configPath = pathFromRelative(localAppDataRoot, game.userConfigRelativePath, 'User config path');
  mkdirSync(dirname(configPath), { recursive: true });
  const contents = existsSync(configPath) ? readFileSync(configPath, 'utf8') : '';
  const temporaryPath = availablePath(`${configPath}.game-translate-${timestamp}.tmp`);
  writeFileSync(temporaryPath, updateCultureIni(contents, culture), {
    encoding: 'utf8',
    flag: 'wx',
    flush: true,
  });
  return { configPath, temporaryPath, timestamp };
}

function commitCulture(prepared) {
  const hadConfig = existsSync(prepared.configPath);
  const configBackup = hadConfig
    ? availablePath(`${prepared.configPath}.game-translate-backup-${prepared.timestamp}`)
    : null;
  if (hadConfig) renameSync(prepared.configPath, configBackup);
  try {
    renameSync(prepared.temporaryPath, prepared.configPath);
  } catch (error) {
    if (configBackup && existsSync(configBackup)) renameSync(configBackup, prepared.configPath);
    throw error;
  }
  return { configPath: prepared.configPath, configBackup };
}

function rollback(actions) {
  for (const action of actions.reverse()) {
    try {
      action();
    } catch {
      // Preserve the original failure; backups remain available for manual recovery.
    }
  }
}

function rollbackCultureCommit({ configPath, configBackup }) {
  if (existsSync(configPath)) rmSync(configPath, { force: true });
  if (configBackup && existsSync(configBackup)) renameSync(configBackup, configPath);
}

function writeInstallState(game, state) {
  mkdirSync(game.workDir, { recursive: true });
  const destination = join(game.workDir, 'install-state.json');
  const temporary = `${destination}.writing.${process.pid}.${Date.now()}`;
  try {
    writeFileSync(temporary, `${JSON.stringify(state, null, 2)}\n`, {
      encoding: 'utf8', flag: 'wx', flush: true,
    });
    renameSync(temporary, destination);
  } catch (error) {
    rmSync(temporary, { force: true });
    throw error;
  }
}

function readInstallState(game) {
  const path = join(game.workDir, 'install-state.json');
  return existsSync(path) ? JSON.parse(readFileSync(path, 'utf8')) : null;
}

export function activateLocalizationMode({
  game,
  modeName = game.defaultMode,
  localAppDataRoot = process.env.LOCALAPPDATA,
  timestamp = Date.now(),
  verifyInstalled = () => {},
}) {
  const mode = resolveLocalizationMode(game, modeName);
  const artifact = join(mode.distDir, mode.patchName);
  if (!existsSync(artifact)) throw new Error(`Build the ${modeName} patch first: ${artifact}`);
  const verification = assertVerifiedArtifact({
    recordPath: join(mode.distDir, 'verification.json'),
    patchPath: artifact,
    mode: modeName,
    expected: {
      targetCulture: mode.targetCulture,
      virtualPaths: game.localizationTargets.map((target) => localizationPathForCulture(
        target.virtualPath,
        game.sourceCulture,
        mode.targetCulture,
      )),
      pakVersion: game.pakVersion,
      pathHashSeed: game.pakPathHashSeed,
      requireCompanions: game.requiresIoStoreCompanions === true,
    },
  });
  const preparedCulture = prepareCulture(game, localAppDataRoot, mode.targetCulture, timestamp);

  const paksDirectory = pathFromRelative(game.gamePath, game.paksRelativePath, 'Pak path outside game');
  mkdirSync(paksDirectory, { recursive: true });
  const disabledPatches = [];
  const destination = join(paksDirectory, mode.patchName);
  let patchBackup = null;
  const rollbackActions = [];
  try {
    for (const [otherName, otherMode] of Object.entries(game.modes)) {
      if (otherName === modeName) continue;
      const disabled = disableArtifacts(paksDirectory, [
        otherMode.patchName,
        otherMode.patchName.replace(/\.pak$/i, '.utoc'),
        otherMode.patchName.replace(/\.pak$/i, '.ucas'),
      ], timestamp);
      for (const item of disabled) {
        disabledPatches.push(item.disabledPath);
        rollbackActions.push(() => renameSync(item.disabledPath, item.active));
      }
    }

    if (existsSync(destination)) {
      const backupDirectory = join(game.workDir, 'backups');
      mkdirSync(backupDirectory, { recursive: true });
      patchBackup = availablePath(join(backupDirectory, `${mode.patchName}.${timestamp}.bak`));
      copyFileSync(destination, patchBackup);
      rollbackActions.push(() => copyFileSync(patchBackup, destination));
    } else {
      rollbackActions.push(() => rmSync(destination, { force: true }));
    }
    copyFileSync(artifact, destination);
    if (sha256File(destination) !== verification.sha256) {
      throw new Error(`Installed artifact hash mismatch: ${destination}`);
    }
    for (const companion of verification.companions ?? []) {
      const source = join(mode.distDir, companion.name);
      const companionDestination = join(paksDirectory, companion.name);
      if (existsSync(companionDestination)) {
        const backupDirectory = join(game.workDir, 'backups');
        mkdirSync(backupDirectory, { recursive: true });
        const backup = availablePath(join(backupDirectory, `${companion.name}.${timestamp}.bak`));
        copyFileSync(companionDestination, backup);
        rollbackActions.push(() => copyFileSync(backup, companionDestination));
      } else {
        rollbackActions.push(() => rmSync(companionDestination, { force: true }));
      }
      copyFileSync(source, companionDestination);
      if (sha256File(companionDestination) !== companion.sha256) {
        throw new Error(`Installed companion hash mismatch: ${companionDestination}`);
      }
    }
    verifyInstalled({ destination, verification });
    const cultureResult = commitCulture(preparedCulture);
    rollbackActions.push(() => rollbackCultureCommit(cultureResult));
    writeInstallState(game, {
      version: 2,
      active: true,
      installedAt: new Date().toISOString(),
      mode: modeName,
      artifact: verification,
      installedFiles: artifactNames(mode.patchName, verification),
      destination,
      patchBackup,
      disabledPatches,
      culture: cultureResult,
    });
    return {
      mode: modeName,
      destination,
      patchBackup,
      disabledPatches,
      verification,
      ...cultureResult,
    };
  } catch (error) {
    rollback(rollbackActions);
    if (existsSync(preparedCulture.temporaryPath)) rmSync(preparedCulture.temporaryPath, { force: true });
    throw error;
  }
}

export function deactivateLocalizationModes({
  game,
  localAppDataRoot = process.env.LOCALAPPDATA,
  timestamp = Date.now(),
}) {
  const previousState = readInstallState(game);
  const paksDirectory = pathFromRelative(game.gamePath, game.paksRelativePath, 'Pak path outside game');
  const preparedCulture = prepareCulture(game, localAppDataRoot, game.sourceCulture, timestamp);
  const disabledPatches = [];
  const rollbackActions = [];
  try {
    for (const mode of Object.values(game.modes)) {
      const disabled = disableArtifacts(paksDirectory, [
        mode.patchName,
        mode.patchName.replace(/\.pak$/i, '.utoc'),
        mode.patchName.replace(/\.pak$/i, '.ucas'),
      ], timestamp);
      for (const item of disabled) {
        disabledPatches.push(item.disabledPath);
        rollbackActions.push(() => renameSync(item.disabledPath, item.active));
      }
    }
    const cultureResult = commitCulture(preparedCulture);
    rollbackActions.push(() => rollbackCultureCommit(cultureResult));
    writeInstallState(game, {
      version: 2,
      active: false,
      disabledAt: new Date().toISOString(),
      artifact: previousState?.artifact ?? null,
      disabledFiles: disabledPatches,
      culture: cultureResult,
    });
    return { disabledPatches, ...cultureResult };
  } catch (error) {
    rollback(rollbackActions);
    if (existsSync(preparedCulture.temporaryPath)) rmSync(preparedCulture.temporaryPath, { force: true });
    throw error;
  }
}
