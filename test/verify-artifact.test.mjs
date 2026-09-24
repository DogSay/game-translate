import assert from 'node:assert/strict';
import { afterEach, test } from 'node:test';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const temporaryDirectories = [];
afterEach(() => {
  for (const directory of temporaryDirectories.splice(0)) {
    rmSync(directory, { recursive: true, force: true });
  }
});

async function loadModule() {
  return import('../src/workflows/verify-artifact.mjs').catch(() => ({}));
}

const info = [
  'mount point: ../../../',
  'version: V11',
  'compression: Zlib',
  'path hash seed: Some(D8195615)',
  '2 file entries',
].join('\n');

test('validates repak metadata and exact virtual paths', async () => {
  const { verifyRepakInfo, verifyRepakList } = await loadModule();
  assert.deepEqual(verifyRepakInfo(info, {
    version: 'V11',
    pathHashSeed: '0xD8195615',
    fileCount: 2,
  }), {
    mountPoint: '../../../',
    version: 'V11',
    compression: 'Zlib',
    pathHashSeed: 'D8195615',
    fileCount: 2,
  });
  assert.deepEqual(
    verifyRepakList('B/zh-Hant/B.locres\nA/zh-Hant/A.locres\n', [
      'A/zh-Hant/A.locres',
      'B/zh-Hant/B.locres',
    ]),
    ['A/zh-Hant/A.locres', 'B/zh-Hant/B.locres'],
  );
});

test('rejects unexpected pak metadata or virtual paths', async () => {
  const { verifyRepakInfo, verifyRepakList } = await loadModule();
  assert.throws(
    () => verifyRepakInfo(info.replace('V11', 'V10'), { version: 'V11', pathHashSeed: '0xD8195615', fileCount: 2 }),
    /pak version/i,
  );
  assert.throws(
    () => verifyRepakList('A/zh-Hans/A.locres\n', ['A/zh-Hant/A.locres']),
    /virtual paths/i,
  );
});

test('verifies staged files against an unpacked patch and records their hashes', async () => {
  const { verifyUnpackedFiles } = await loadModule();
  const root = mkdtempSync(join(tmpdir(), 'game-translate-roundtrip-'));
  temporaryDirectories.push(root);
  const stage = join(root, 'stage');
  const unpacked = join(root, 'unpacked');
  const virtualPath = 'Game/Localization/zh-Hant/Game.locres';
  for (const base of [stage, unpacked]) {
    const file = join(base, ...virtualPath.split('/'));
    mkdirSync(join(file, '..'), { recursive: true });
    writeFileSync(file, 'same locres');
  }

  const hashes = verifyUnpackedFiles(stage, unpacked, [virtualPath]);
  assert.match(hashes[virtualPath], /^[A-F0-9]{64}$/);
  writeFileSync(join(unpacked, ...virtualPath.split('/')), 'tampered');
  assert.throws(() => verifyUnpackedFiles(stage, unpacked, [virtualPath]), /round-trip.*Game\.locres/i);
});

test('requires an artifact hash to match its persisted verification record', async () => {
  const { writeVerificationRecord, assertVerifiedArtifact } = await loadModule();
  const root = mkdtempSync(join(tmpdir(), 'game-translate-record-'));
  temporaryDirectories.push(root);
  const patchPath = join(root, 'patch_P.pak');
  const recordPath = join(root, 'verification.json');
  writeFileSync(patchPath, 'verified patch');

  writeVerificationRecord({
    recordPath,
    patchPath,
    mode: 'native',
    targetCulture: 'zh-Hant',
    virtualPaths: ['Game/zh-Hant/Game.locres'],
    fileHashes: { 'Game/zh-Hant/Game.locres': 'A'.repeat(64) },
    pak: { mountPoint: '../../../', version: 'V11', compression: 'Zlib', pathHashSeed: 'D8195615', fileCount: 1 },
    verifiedAt: '2026-07-17T00:00:00.000Z',
  });

  assert.equal(assertVerifiedArtifact({ recordPath, patchPath, mode: 'native' }).mode, 'native');
  writeFileSync(patchPath, 'changed patch');
  assert.throws(() => assertVerifiedArtifact({ recordPath, patchPath, mode: 'native' }), /hash.*verification record/i);
});

test('binds a verification record to the current manifest culture paths and pak metadata', async () => {
  const { writeVerificationRecord, assertVerifiedArtifact } = await loadModule();
  const root = mkdtempSync(join(tmpdir(), 'game-translate-manifest-record-'));
  temporaryDirectories.push(root);
  const patchPath = join(root, 'patch_P.pak');
  const recordPath = join(root, 'verification.json');
  writeFileSync(patchPath, 'verified patch');
  writeVerificationRecord({
    recordPath,
    patchPath,
    mode: 'native',
    targetCulture: 'zh-Hant',
    virtualPaths: ['Game/zh-Hant/Game.locres'],
    fileHashes: { 'Game/zh-Hant/Game.locres': 'A'.repeat(64) },
    pak: { mountPoint: '../../../', version: 'V11', compression: 'Zlib', pathHashSeed: 'D8195615', fileCount: 1 },
  });
  const base = { recordPath, patchPath, mode: 'native' };

  assert.throws(() => assertVerifiedArtifact({
    ...base,
    expected: {
      targetCulture: 'zh-Hant-TW',
      virtualPaths: ['Game/zh-Hant-TW/Game.locres'],
      pakVersion: 'V11',
      pathHashSeed: '0xD8195615',
    },
  }), /manifest.*target culture/i);
  assert.throws(() => assertVerifiedArtifact({
    ...base,
    expected: {
      targetCulture: 'zh-Hant',
      virtualPaths: ['Game/zh-Hant/Game.locres', 'Legal/zh-Hant/Legal.locres'],
      pakVersion: 'V11',
      pathHashSeed: '0xD8195615',
    },
  }), /manifest.*virtual paths/i);
});

test('binds an IoStore artifact to verified utoc and ucas companions', async () => {
  const { writeVerificationRecord, assertVerifiedArtifact } = await loadModule();
  const root = mkdtempSync(join(tmpdir(), 'game-translate-companions-'));
  temporaryDirectories.push(root);
  const patchPath = join(root, 'patch_P.pak');
  const utocPath = join(root, 'patch_P.utoc');
  const ucasPath = join(root, 'patch_P.ucas');
  const recordPath = join(root, 'verification.json');
  writeFileSync(patchPath, 'verified patch');
  writeFileSync(utocPath, 'verified utoc');
  writeFileSync(ucasPath, 'verified ucas');

  const record = writeVerificationRecord({
    recordPath,
    patchPath,
    companionPaths: [utocPath, ucasPath],
    mode: 'compat',
    targetCulture: 'zh-Hans',
    virtualPaths: ['Game/zh-Hans/Game.locres'],
    fileHashes: { 'Game/zh-Hans/Game.locres': 'A'.repeat(64) },
    pak: { mountPoint: '../../../', version: 'V11', compression: 'Zlib', pathHashSeed: 'D8195615', fileCount: 1 },
  });

  assert.equal(record.version, 2);
  assert.deepEqual(record.companions.map((item) => item.name), ['patch_P.ucas', 'patch_P.utoc']);
  assert.equal(assertVerifiedArtifact({ recordPath, patchPath, mode: 'compat' }).companions.length, 2);
  writeFileSync(utocPath, 'tampered utoc');
  assert.throws(() => assertVerifiedArtifact({ recordPath, patchPath, mode: 'compat' }), /companion.*hash/i);
});

test('rejects IoStore companions that do not match the patch stem and extension pair', async () => {
  const { writeVerificationRecord, assertVerifiedArtifact } = await loadModule();
  const root = mkdtempSync(join(tmpdir(), 'game-translate-mismatched-companions-'));
  temporaryDirectories.push(root);
  const patchPath = join(root, 'patch_P.pak');
  const first = join(root, 'other_P.utoc');
  const second = join(root, 'patch_P-copy.utoc');
  const recordPath = join(root, 'verification.json');
  writeFileSync(patchPath, 'patch');
  writeFileSync(first, 'first');
  writeFileSync(second, 'second');
  writeVerificationRecord({
    recordPath, patchPath, companionPaths: [first, second], mode: 'compat', targetCulture: 'zh-Hans',
    virtualPaths: [], fileHashes: {},
    pak: { mountPoint: '../../../', version: 'V11', compression: 'Zlib', pathHashSeed: 'D8195615', fileCount: 0 },
  });
  assert.throws(() => assertVerifiedArtifact({ recordPath, patchPath, mode: 'compat' }), /companion.*match|one.*utoc.*ucas/i);
});

test('rejects a one-file verification record when the manifest requires IoStore companions', async () => {
  const { writeVerificationRecord, assertVerifiedArtifact } = await loadModule();
  const root = mkdtempSync(join(tmpdir(), 'game-translate-required-companions-'));
  temporaryDirectories.push(root);
  const patchPath = join(root, 'patch_P.pak');
  const recordPath = join(root, 'verification.json');
  writeFileSync(patchPath, 'patch');
  writeVerificationRecord({ recordPath, patchPath, mode: 'compat', targetCulture: 'zh-Hans',
    virtualPaths: [], fileHashes: {}, pak: { mountPoint: '../../../', version: 'V11', compression: 'Zlib', pathHashSeed: 'D8195615', fileCount: 0 } });
  assert.throws(() => assertVerifiedArtifact({
    recordPath, patchPath, mode: 'compat', expected: {
      targetCulture: 'zh-Hans', virtualPaths: [], pakVersion: 'V11', pathHashSeed: 'D8195615', requireCompanions: true,
    },
  }), /requires.*companion/i);
});
