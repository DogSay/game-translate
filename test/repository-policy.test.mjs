import assert from 'node:assert/strict';
import { test } from 'node:test';

async function loadModule() {
  return import('../src/core/repository-policy.mjs').catch(() => ({}));
}

test('accepts the intended public source tree', async () => {
  const { auditRepositoryPaths } = await loadModule();

  assert.equal(typeof auditRepositoryPaths, 'function');
  assert.deepEqual(auditRepositoryPaths([
    'README.md',
    'LICENSE',
    'THIRD_PARTY_NOTICES.md',
    'CONTRIBUTING.md',
    'SECURITY.md',
    '.tools/manifest.json',
    'src/cli.mjs',
    'desktop/GameTranslate.App/MainForm.cs',
    'test/fixtures/synthetic.locres',
  ]), []);
});

test('rejects local game payloads and generated artifacts', async () => {
  const { auditRepositoryPaths } = await loadModule();

  assert.equal(typeof auditRepositoryPaths, 'function');
  assert.deepEqual(auditRepositoryPaths([
    'README.md',
    'work/the-mound/base.pak',
    'dist/portable/GameTranslate.exe',
    'tmp/pdfs/page-1.png',
    '.tools/repak-0.2.3/repak.exe',
    'Crawl_汉化文件v0.5.0/README.pdf',
    'oodle-data-shared.dll',
    'games/the-mound/shipping.locres',
  ]), [
    '.tools/repak-0.2.3/repak.exe: downloaded tool payload must stay untracked',
    'Crawl_汉化文件v0.5.0/README.pdf: downloaded translation/game payload must stay untracked',
    'dist/portable/GameTranslate.exe: generated output must stay untracked',
    'games/the-mound/shipping.locres: binary game payload must stay untracked',
    'oodle-data-shared.dll: binary game payload must stay untracked',
    'tmp/pdfs/page-1.png: temporary output must stay untracked',
    'work/the-mound/base.pak: local work data must stay untracked',
  ]);
});

test('reports missing public project documents', async () => {
  const { auditRequiredDocuments } = await loadModule();

  assert.equal(typeof auditRequiredDocuments, 'function');
  assert.deepEqual(auditRequiredDocuments(['README.md', 'LICENSE']), [
    'CONTRIBUTING.md is required',
    'SECURITY.md is required',
    'THIRD_PARTY_NOTICES.md is required',
  ]);
});

test('combines document and tracked-path failures for the release gate', async () => {
  const { auditPublicRepository } = await loadModule();

  assert.equal(typeof auditPublicRepository, 'function');
  assert.deepEqual(auditPublicRepository([
    'README.md',
    'LICENSE',
    'dist/portable/GameTranslate.exe',
  ]), [
    'CONTRIBUTING.md is required',
    'SECURITY.md is required',
    'THIRD_PARTY_NOTICES.md is required',
    'dist/portable/GameTranslate.exe: generated output must stay untracked',
  ]);
});
