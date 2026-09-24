import { posix, win32 } from 'node:path';

function safeVirtualPath(virtualPath) {
  const normalized = virtualPath.replaceAll('\\', '/');
  if (/^[A-Za-z]:/.test(normalized) || normalized.startsWith('/') || normalized.split('/').includes('..')) {
    throw new Error(`Unsafe Unreal virtual path: ${virtualPath}`);
  }
  return normalized;
}

export function extractionArguments({ gamePath, outputDirectory, virtualPath }) {
  const normalized = safeVirtualPath(virtualPath);
  const output = `${outputDirectory.replaceAll('\\', '/').replace(/\/$/, '')}/`;
  return [
    gamePath,
    output,
    `--path=${posix.dirname(normalized)}/`,
    '--no-parallel',
    '--extract-locres',
  ];
}

export function stagingPath(stageDirectory, virtualPath) {
  const normalized = safeVirtualPath(virtualPath);
  return win32.join(stageDirectory, ...normalized.split('/'));
}

export function localizationPathForCulture(virtualPath, sourceCulture, targetCulture) {
  const normalized = safeVirtualPath(virtualPath);
  const segments = normalized.split('/');
  const matchingIndexes = segments
    .map((segment, index) => (segment === sourceCulture ? index : -1))
    .filter((index) => index >= 0);
  if (matchingIndexes.length !== 1) {
    throw new Error(`Expected exactly one source culture segment (${sourceCulture}) in: ${virtualPath}`);
  }
  segments[matchingIndexes[0]] = targetCulture;
  return safeVirtualPath(segments.join('/'));
}

export function pakPackArguments({ stageDirectory, outputPak, version, pathHashSeed }) {
  const seed = BigInt(pathHashSeed);
  if (seed < 0n || seed > 0xFFFF_FFFF_FFFF_FFFFn) throw new Error(`Invalid pak path hash seed: ${pathHashSeed}`);
  return [
    'pack',
    '--version', version,
    '--path-hash-seed', seed.toString(10),
    '--compression', 'Zlib',
    stageDirectory,
    outputPak,
  ];
}

export function normalizeRetocVersion(value) {
  const text = String(value ?? '').trim();
  const match = text.match(/(?:GAME_)?UE([45])[_\.\-]?(\d+)/i) ?? text.match(/^([45])\.(\d+)(?:\.|$)/);
  if (!match) throw new Error(`Unsupported Unreal Engine version for IoStore companions: ${value}`);
  const major = Number(match[1]);
  const minor = Number(match[2]);
  const supported = (major === 4 && minor >= 25 && minor <= 27)
    || (major === 5 && minor >= 0 && minor <= 7);
  if (!supported) throw new Error(`Unsupported Unreal Engine version for IoStore companions: ${value}`);
  return `UE${major}_${minor}`;
}

export function iostoreCompanionArguments({ patchPath, outputUtoc, engineVersion }) {
  return ['to-zen', patchPath, outputUtoc, '--version', normalizeRetocVersion(engineVersion)];
}
