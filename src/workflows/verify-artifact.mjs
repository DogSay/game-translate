import { createHash } from 'node:crypto';
import { existsSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { basename, join } from 'node:path';

export function sha256File(path) {
  return createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase();
}

function valueFromLine(output, label) {
  const match = output.match(new RegExp(`^${label}:\\s*(.+)$`, 'im'));
  return match?.[1]?.trim();
}

export function verifyRepakInfo(output, expected) {
  const mountPoint = valueFromLine(output, 'mount point');
  const version = valueFromLine(output, 'version');
  const compression = valueFromLine(output, 'compression');
  const seedMatch = output.match(/^path hash seed:\s*Some\(([0-9A-F]+)\)/im);
  const countMatch = output.match(/^(\d+) file entries$/im);
  const pathHashSeed = seedMatch?.[1]?.toUpperCase();
  const fileCount = Number(countMatch?.[1]);
  const expectedSeed = String(expected.pathHashSeed).replace(/^0x/i, '').toUpperCase();
  if (version !== expected.version) throw new Error(`Unexpected pak version: ${version}`);
  if (mountPoint !== '../../../') throw new Error(`Unexpected pak mount point: ${mountPoint}`);
  if (compression !== 'Zlib') throw new Error(`Unexpected pak compression: ${compression}`);
  if (pathHashSeed !== expectedSeed) throw new Error(`Unexpected pak path hash seed: ${pathHashSeed}`);
  if (fileCount !== expected.fileCount) throw new Error(`Unexpected pak file count: ${fileCount}`);
  return { mountPoint, version, compression, pathHashSeed, fileCount };
}

export function verifyRepakList(output, expectedPaths) {
  const actual = output.split(/\r?\n/).map((line) => line.trim()).filter(Boolean).sort();
  const expected = [...expectedPaths].sort();
  if (JSON.stringify(actual) !== JSON.stringify(expected)) {
    throw new Error('Pak virtual paths do not match the expected localization paths');
  }
  return actual;
}

export function verifyUnpackedFiles(stageDirectory, unpackedDirectory, virtualPaths) {
  const hashes = {};
  for (const virtualPath of virtualPaths) {
    const segments = virtualPath.replaceAll('\\', '/').split('/');
    if (segments.includes('..') || virtualPath.startsWith('/') || /^[A-Za-z]:/.test(virtualPath)) {
      throw new Error(`Unsafe virtual path during round-trip verification: ${virtualPath}`);
    }
    const staged = join(stageDirectory, ...segments);
    const unpacked = join(unpackedDirectory, ...segments);
    if (!existsSync(staged) || !existsSync(unpacked)) {
      throw new Error(`Missing round-trip file: ${virtualPath}`);
    }
    const stagedHash = sha256File(staged);
    if (sha256File(unpacked) !== stagedHash) {
      throw new Error(`Pak round-trip hash mismatch: ${virtualPath}`);
    }
    hashes[virtualPath] = stagedHash;
  }
  return hashes;
}

export function writeVerificationRecord({
  recordPath,
  patchPath,
  mode,
  targetCulture,
  virtualPaths,
  fileHashes,
  pak,
  companionPaths = [],
  verifiedAt = new Date().toISOString(),
}) {
  const companions = companionPaths.map((path) => ({
    name: basename(path),
    bytes: statSync(path).size,
    sha256: sha256File(path),
  })).sort((left, right) => left.name.localeCompare(right.name));
  const record = {
    version: companions.length > 0 ? 2 : 1,
    mode,
    targetCulture,
    patchName: basename(patchPath),
    patchBytes: statSync(patchPath).size,
    sha256: sha256File(patchPath),
    virtualPaths: [...virtualPaths].sort(),
    fileHashes,
    pak,
    ...(companions.length > 0 ? { companions } : {}),
    verifiedAt,
  };
  writeFileSync(recordPath, `${JSON.stringify(record, null, 2)}\n`, { encoding: 'utf8', flush: true });
  return record;
}

export function assertVerifiedArtifact({ recordPath, patchPath, mode, expected }) {
  if (!existsSync(recordPath)) throw new Error(`Missing artifact verification record: ${recordPath}`);
  const record = JSON.parse(readFileSync(recordPath, 'utf8'));
  if (![1, 2].includes(record.version) || record.mode !== mode || record.patchName !== basename(patchPath)) {
    throw new Error(`Artifact verification record does not describe ${mode}: ${patchPath}`);
  }
  if (record.sha256 !== sha256File(patchPath)) {
    throw new Error('Artifact hash does not match its verification record');
  }
  if (expected?.requireCompanions && record.version !== 2) {
    throw new Error('Manifest requires verified IoStore companions for this artifact');
  }
  if (record.version === 2) {
    if (!Array.isArray(record.companions) || record.companions.length !== 2) {
      throw new Error('IoStore artifact verification record must describe both companions');
    }
    const patchStem = basename(patchPath).replace(/\.pak$/i, '');
    const extensions = record.companions.map((item) => item.name.match(/\.(utoc|ucas)$/i)?.[1]?.toLowerCase()).sort();
    if (JSON.stringify(extensions) !== JSON.stringify(['ucas', 'utoc'])
      || record.companions.some((item) => item.name.replace(/\.(?:utoc|ucas)$/i, '') !== patchStem)) {
      throw new Error('Artifact companions must match the patch stem and contain one utoc and one ucas');
    }
    const patchDirectory = join(patchPath, '..');
    for (const companion of record.companions) {
      if (basename(companion.name) !== companion.name || !/\.(?:utoc|ucas)$/i.test(companion.name)) {
        throw new Error(`Unsafe artifact companion name: ${companion.name}`);
      }
      const companionPath = join(patchDirectory, companion.name);
      if (!existsSync(companionPath)) throw new Error(`Missing artifact companion: ${companion.name}`);
      if (statSync(companionPath).size !== companion.bytes || sha256File(companionPath) !== companion.sha256) {
        throw new Error(`Artifact companion hash does not match its verification record: ${companion.name}`);
      }
    }
  }
  if (expected) {
    if (record.targetCulture !== expected.targetCulture) {
      throw new Error('Verification record does not match the manifest target culture');
    }
    const recordPaths = [...(record.virtualPaths ?? [])].sort();
    const expectedPaths = [...expected.virtualPaths].sort();
    if (JSON.stringify(recordPaths) !== JSON.stringify(expectedPaths)) {
      throw new Error('Verification record does not match the manifest virtual paths');
    }
    const expectedSeed = String(expected.pathHashSeed).replace(/^0x/i, '').toUpperCase();
    if (record.pak?.version !== expected.pakVersion
      || record.pak?.pathHashSeed !== expectedSeed
      || record.pak?.fileCount !== expectedPaths.length) {
      throw new Error('Verification record does not match the manifest pak metadata');
    }
  }
  return record;
}
