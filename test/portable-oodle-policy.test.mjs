import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { test } from 'node:test';

const project = readFileSync(new URL('../desktop/GameTranslate.App/GameTranslate.App.csproj', import.meta.url), 'utf8');
const installer = readFileSync(new URL('../desktop/GameTranslate.App/ToolInstaller.cs', import.meta.url), 'utf8');
const appProgram = readFileSync(new URL('../desktop/GameTranslate.App/Program.cs', import.meta.url), 'utf8');
const oodleNames = ['oo2core_9_win64.dll', 'oodle-data-shared.dll'];
const manifest = JSON.parse(readFileSync(new URL('../.tools/manifest.json', import.meta.url), 'utf8'));

test('portable executable does not embed Oodle binaries', () => {
  const embeddedResources = [...project.matchAll(/<EmbeddedResource\s+[^>]*Include="([^"]+)"/g)]
    .map((match) => match[1].replaceAll('\\', '/'));

  for (const name of oodleNames) {
    assert.equal(embeddedResources.some((resource) => resource.endsWith(`/${name}`)), false, `${name} is embedded`);
  }
});

test('tool installer does not require an embedded Oodle resource', () => {
  const resourceSection = installer.match(/private static readonly string\[\] Resources\s*=\s*\[([\s\S]*?)\];/);
  assert.ok(resourceSection, 'tool resource list is missing');

  for (const name of oodleNames) {
    assert.equal(resourceSection[1].includes(`"${name}"`), false, `${name} is required from the assembly`);
  }
});

test('portable workflow launches UEExtractor apphost from the tools directory', () => {
  assert.match(project, /<EmbeddedResource\s+[^>]*Include="[^"]*UEExtractor\.exe"[^>]*LogicalName="Tools\.UEExtractor\.exe"/);
  assert.match(project, /<EmbeddedResource\s+[^>]*Include="[^"]*UEExtractor\.runtimeconfig\.json"[^>]*LogicalName="Tools\.UEExtractor\.runtimeconfig\.json"/);
  assert.match(installer, /"UEExtractor\.exe"/);
  assert.match(installer, /Path\.Combine\(directory, "UEExtractor\.exe"\),\s*Array\.Empty<string>\(\)/);
});

test('native zlib input is pinned to the verified upstream archive and DLL', () => {
  assert.ok(manifest.zlibNg, 'zlib-ng build input is not pinned');
  assert.equal(manifest.zlibNg.version, '2.3.2');
  assert.equal(manifest.zlibNg.path, 'zlib-ng2.dll');
  assert.equal(manifest.zlibNg.sha256, '8fd35b640032217aef52017c0683963ba51341acdb2f7b24e6c7a7d6ec893412');
  assert.equal(manifest.zlibNg.fileSha256, '454be2f3d10f804ace577198401431db5e95d0286b59589bc28a40085388e7c2');
  assert.match(manifest.zlibNg.url, /github\.com\/zlib-ng\/zlib-ng\/releases\/download\/2\.3\.2\/zlib-ng-win-x86-64\.zip$/);
});

test('portable EXE installs the exact third-party license texts', () => {
  for (const [name, source] of [
    ['UEExtractor-LICENSE.txt', '../licenses/UEExtractor-MIT.txt'],
    ['CUE4Parse-LICENSE.txt', '../licenses/CUE4Parse-APACHE.txt'],
    ['zlib-ng-LICENSE.txt', '../licenses/zlib-ng-LICENSE.txt'],
  ]) {
    const path = new URL(source, import.meta.url);
    assert.ok(existsSync(path), `${name} source license is missing`);
    assert.ok(readFileSync(path, 'utf8').length > 100, `${name} source license is empty`);
    assert.ok(project.includes(`LogicalName="Tools.${name}"`), `${name} is not embedded`);
    assert.ok(installer.includes(`"${name}"`), `${name} is not installed`);
  }
});

test('tool self-test verifies native runtimes before launching bundled tools', () => {
  const selfTest = appProgram.match(/private static async Task<int> TestTools\(string root\)([\s\S]*?)private static async Task<int> TestApi/);
  assert.ok(selfTest, 'tool self-test not found');
  assert.match(selfTest[1], /NativeRuntimePreflight\.VerifyExistingRepakRuntime/);
  assert.match(selfTest[1], /PinnedZipRuntime\.EnsureAsync/);
  assert.doesNotMatch(selfTest[1], /UeExtractorHost,\s*\[[^\]]*"--help"/);
});
