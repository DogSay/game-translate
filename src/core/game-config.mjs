import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

function validateVirtualPath(value, field = 'localizationTargets.virtualPath') {
  if (typeof value !== 'string' || value.length === 0) {
    throw new Error(`${field} is required`);
  }
  const normalized = value.replaceAll('\\', '/');
  if (/^[A-Za-z]:/.test(normalized) || normalized.startsWith('/') || normalized.split('/').includes('..')) {
    throw new Error(`${field} traversal or absolute path is not allowed: ${value}`);
  }
  return normalized;
}

function validatePatchName(value) {
  if (typeof value !== 'string' || !/^[^\\/]+_P\.pak$/i.test(value)) {
    throw new Error(`Localization mode patchName must be a standalone _P.pak filename: ${value}`);
  }
  return value;
}

function normalizeModes(config) {
  const rawModes = config.modes ?? (
    config.targetCulture && config.patchName
      ? { compat: { targetCulture: config.targetCulture, patchName: config.patchName } }
      : {}
  );
  const entries = Object.entries(rawModes);
  if (entries.length === 0) return {};

  const modes = Object.fromEntries(entries.map(([name, mode]) => {
    if (!/^[a-z0-9][a-z0-9-]*$/.test(name)) throw new Error(`Invalid localization mode name: ${name}`);
    if (typeof mode?.targetCulture !== 'string' || !/^[A-Za-z0-9-]+$/.test(mode.targetCulture)) {
      throw new Error(`Invalid targetCulture for localization mode ${name}`);
    }
    return [name, {
      targetCulture: mode.targetCulture,
      patchName: validatePatchName(mode.patchName),
    }];
  }));

  const patchNames = Object.values(modes).map((mode) => mode.patchName.toLowerCase());
  if (new Set(patchNames).size !== patchNames.length) {
    throw new Error('Localization mode patch names must be unique');
  }
  return modes;
}

export function resolveLocalizationMode(game, modeName = game.defaultMode) {
  const mode = game.modes?.[modeName];
  if (!mode) throw new Error(`Unknown localization mode: ${modeName}`);
  return {
    name: modeName,
    ...mode,
    distDir: join(game.distDir, modeName),
  };
}

export function loadGameConfig(projectRoot, gameId) {
  const manifestPath = join(projectRoot, 'games', gameId, 'game.json');
  const manifestConfig = JSON.parse(readFileSync(manifestPath, 'utf8'));
  const localConfigPath = join(projectRoot, 'games', gameId, 'game.local.json');
  let localConfig = {};
  if (existsSync(localConfigPath)) {
    localConfig = JSON.parse(readFileSync(localConfigPath, 'utf8'));
    const keys = Object.keys(localConfig);
    if (keys.length !== 1 || keys[0] !== 'gamePath' || typeof localConfig.gamePath !== 'string') {
      throw new Error('game.local.json may override only a string gamePath');
    }
  }
  const config = { ...manifestConfig, ...localConfig };
  if (config.id !== gameId) throw new Error(`Game id mismatch: expected ${gameId}`);
  if (!['unreal', 'unity'].includes(config.engine)) throw new Error(`Unsupported engine: ${config.engine}`);
  if (!Array.isArray(config.localizationTargets) || config.localizationTargets.length === 0) {
    throw new Error('At least one localization target is required');
  }

  const localizationTargets = config.localizationTargets.map((target) => ({
    ...target,
    virtualPath: validateVirtualPath(target.virtualPath),
  }));
  const modes = normalizeModes(config);
  const defaultMode = config.defaultMode ?? Object.keys(modes)[0];
  if (defaultMode && !modes[defaultMode]) throw new Error(`Default localization mode does not exist: ${defaultMode}`);

  return {
    ...config,
    defaultMode,
    modes,
    ...(config.userConfigRelativePath
      ? { userConfigRelativePath: validateVirtualPath(config.userConfigRelativePath, 'userConfigRelativePath') }
      : {}),
    ...(config.paksRelativePath
      ? { paksRelativePath: validateVirtualPath(config.paksRelativePath, 'paksRelativePath') }
      : {}),
    localizationTargets,
    manifestPath,
    projectRoot,
    workDir: join(projectRoot, 'work', gameId),
    distDir: join(projectRoot, 'dist', gameId),
  };
}
