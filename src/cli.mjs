#!/usr/bin/env node
import { copyFileSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { detectEngine } from './core/detect-engine.mjs';
import { loadGameConfig, resolveLocalizationMode } from './core/game-config.mjs';
import { loadToolPaths, runExternal } from './core/external-tools.mjs';
import {
  extractionArguments,
  iostoreCompanionArguments,
  localizationPathForCulture,
  pakPackArguments,
  stagingPath,
} from './adapters/unreal.mjs';
import { ZhConvertClient } from './providers/zhconvert.mjs';
import { convertCsvFile } from './workflows/convert-csv-file.mjs';
import { TranslationMemory, translationEntriesFromDocument } from './core/translation-memory.mjs';
import { activateLocalizationMode, deactivateLocalizationModes } from './workflows/manage-mode.mjs';
import { normalizeExtractedCsvFile, promoteArchiveExtraction } from './formats/ueextractor-csv.mjs';
import { validateTranslatedCsv } from './workflows/validate-localization.mjs';
import { patchTranslatedLocres } from './formats/locres-surgery.mjs';
import {
  verifyRepakInfo,
  verifyRepakList,
  verifyUnpackedFiles,
  writeVerificationRecord,
} from './workflows/verify-artifact.mjs';

const projectRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const [command = 'help', gameId = 'the-mound', requestedMode] = process.argv.slice(2);

function config() {
  return loadGameConfig(projectRoot, gameId);
}

function tools() {
  return loadToolPaths(projectRoot);
}

function ensureDirectory(path) {
  mkdirSync(path, { recursive: true });
}

function resetProjectDirectory(path) {
  const relative = resolve(path).slice(resolve(projectRoot).length);
  if (!relative.startsWith('\\') || relative.includes('..')) {
    throw new Error(`Refusing to reset a directory outside the project: ${path}`);
  }
  rmSync(path, { recursive: true, force: true });
  ensureDirectory(path);
}

function doctor() {
  const game = config();
  const detected = detectEngine(game.gamePath);
  const toolPaths = tools();
  const checks = {
    gamePath: existsSync(game.gamePath),
    engineMatches: detected.engine === game.engine,
    tools: Object.fromEntries(Object.entries(toolPaths).map(([name, path]) => [name, existsSync(path)])),
  };
  console.log(JSON.stringify({ game: game.title, detected, checks }, null, 2));
  if (!checks.gamePath || !checks.engineMatches || Object.values(checks.tools).includes(false)) process.exitCode = 1;
}

function scan() {
  const game = config();
  const detected = detectEngine(game.gamePath);
  const paks = join(game.gamePath, game.paksRelativePath);
  const archives = existsSync(paks)
    ? readdirSync(paks).filter((name) => /\.(pak|utoc|ucas)$/i.test(name)).map((name) => ({
        name,
        bytes: statSync(join(paks, name)).size,
      }))
    : [];
  console.log(JSON.stringify({ game: game.title, detected, paks, archives }, null, 2));
}

function extract() {
  const game = config();
  if (game.engine !== 'unreal') throw new Error('The extract command currently requires an Unreal adapter');
  const sourceDirectory = join(game.workDir, 'source');
  ensureDirectory(sourceDirectory);
  const { UEExtractor } = tools();
  for (const target of game.localizationTargets) {
    console.log(`Extracting ${target.name}...`);
    runExternal(UEExtractor, extractionArguments({
      gamePath: game.gamePath,
      outputDirectory: sourceDirectory,
      virtualPath: target.virtualPath,
    }));
    if (game.sourceArchive) {
      const promoted = promoteArchiveExtraction({
        sourceDirectory,
        targetName: target.name,
        sourceArchive: game.sourceArchive,
      });
      if (promoted) console.log(`${target.name}: selected base archive ${game.sourceArchive}`);
    }
    const csv = join(sourceDirectory, `${target.name}.csv`);
    const hashes = join(sourceDirectory, `${target.name}.locreshashes`);
    if (existsSync(csv) && existsSync(hashes)) {
      const repairs = normalizeExtractedCsvFile(csv, hashes);
      if (repairs > 0) console.log(`${target.name}: repaired ${repairs} multiline locres key(s)`);
    }
  }
}

async function convert() {
  const game = config();
  const sourceDirectory = join(game.workDir, 'source');
  const translatedDirectory = join(game.workDir, 'translated');
  ensureDirectory(translatedDirectory);
  const client = new ZhConvertClient();
  const memory = new TranslationMemory(join(game.workDir, 'translation-memory', 'zhconvert-taiwan.json'));
  console.log('本程式使用了繁化姬的 API 服務；繁化姬商用必須付費：https://zhconvert.org/');

  for (const target of game.localizationTargets) {
    const sourceCsv = join(sourceDirectory, `${target.name}.csv`);
    const translatedCsv = join(translatedDirectory, `${target.name}.csv`);
    const inputCsv = existsSync(translatedCsv) ? translatedCsv : sourceCsv;
    const sidecar = join(sourceDirectory, `${target.name}.locreshashes`);
    if (existsSync(inputCsv) && existsSync(sidecar)) normalizeExtractedCsvFile(inputCsv, sidecar);
    console.log(`Converting ${target.name}...`);
    const stats = await convertCsvFile({
      inputPath: inputCsv,
      outputPath: translatedCsv,
      converter: (text) => memory.convert(text, (source) => client.convert(source)),
      batchConverter: (texts) => memory.convertBatch(texts, (sources) => client.convertBatch(sources)),
      batchSize: 100,
    });
    if (existsSync(sidecar)) copyFileSync(sidecar, join(translatedDirectory, `${target.name}.locreshashes`));
    console.log(`${target.name}: ${stats.converted} filled, ${stats.preserved} preserved`);
  }
}

function build() {
  const game = config();
  const mode = resolveLocalizationMode(game, requestedMode ?? game.defaultMode);
  const translatedDirectory = join(game.workDir, 'translated');
  const buildDirectory = join(game.workDir, 'built', mode.name);
  const templateDirectory = join(buildDirectory, 'templates');
  const stageDirectory = join(buildDirectory, 'stage');
  resetProjectDirectory(buildDirectory);
  resetProjectDirectory(mode.distDir);
  ensureDirectory(stageDirectory);
  const { repak, retoc } = tools();
  const virtualPaths = [];
  const sourcePak = join(game.gamePath, game.paksRelativePath, game.sourceArchive);
  if (!existsSync(sourcePak)) throw new Error(`Missing source pak: ${sourcePak}`);
  const memoryPath = join(game.workDir, 'translation-memory', 'zhconvert-taiwan.json');
  const memory = existsSync(memoryPath)
    ? translationEntriesFromDocument(JSON.parse(readFileSync(memoryPath, 'utf8').replace(/^\uFEFF/, '')))
    : {};

  for (const target of game.localizationTargets) {
    const csv = join(translatedDirectory, `${target.name}.csv`);
    const sourceCsv = join(game.workDir, 'source', `${target.name}.csv`);
    if (!existsSync(csv)) throw new Error(`Missing translated CSV: ${csv}`);
    if (!existsSync(sourceCsv)) throw new Error(`Missing immutable source CSV: ${sourceCsv}`);
    console.log(`Surgically patching ${target.name}.locres...`);
    const { entries: expectedEntries } = validateTranslatedCsv(
      readFileSync(sourceCsv, 'utf8'),
      readFileSync(csv, 'utf8'),
      target.name,
    );
    runExternal(repak, ['unpack', '-o', templateDirectory, '-i', target.virtualPath, sourcePak], { capture: true });
    const templatePath = stagingPath(templateDirectory, target.virtualPath);
    if (!existsSync(templatePath)) throw new Error(`${target.name}: source locres was not extracted from ${game.sourceArchive}`);
    const template = readFileSync(templatePath);
    const result = patchTranslatedLocres(template, readFileSync(csv, 'utf8'), memory, {
      compat: mode.name === 'compat',
    });
    if (!result.bytes.subarray(0, Number(template.readBigInt64LE(17)) + 4)
      .equals(template.subarray(0, Number(template.readBigInt64LE(17)) + 4))) {
      throw new Error(`${target.name}: locres surgery changed protected header or hash-table bytes`);
    }
    console.log(`${target.name}: locres v${result.version}, ${result.replaced}/${result.stringCount} strings replaced`);
    const destinationVirtualPath = localizationPathForCulture(
      target.virtualPath,
      game.sourceCulture,
      mode.targetCulture,
    );
    virtualPaths.push(destinationVirtualPath);
    const destination = stagingPath(stageDirectory, destinationVirtualPath);
    ensureDirectory(dirname(destination));
    writeFileSync(destination, result.bytes, { flush: true });
  }

  const patch = join(mode.distDir, mode.patchName);
  runExternal(repak, pakPackArguments({
    stageDirectory,
    outputPak: patch,
    version: game.pakVersion,
    pathHashSeed: game.pakPathHashSeed,
  }));
  const infoResult = runExternal(repak, ['info', patch], { capture: true });
  const pak = verifyRepakInfo(`${infoResult.stdout ?? ''}\n${infoResult.stderr ?? ''}`, {
    version: game.pakVersion,
    pathHashSeed: game.pakPathHashSeed,
    fileCount: virtualPaths.length,
  });
  const listResult = runExternal(repak, ['list', patch], { capture: true });
  verifyRepakList(`${listResult.stdout ?? ''}\n${listResult.stderr ?? ''}`, virtualPaths);
  const verificationDirectory = join(game.workDir, 'verification', mode.name);
  resetProjectDirectory(verificationDirectory);
  runExternal(repak, ['unpack', '-o', verificationDirectory, patch], { capture: true });
  const fileHashes = verifyUnpackedFiles(stageDirectory, verificationDirectory, virtualPaths);
  const companionPaths = [];
  const sourceUtoc = sourcePak.replace(/\.pak$/i, '.utoc');
  if (game.requiresIoStoreCompanions && !existsSync(sourceUtoc)) {
    throw new Error(`This game requires IoStore companions but the source utoc is missing: ${sourceUtoc}`);
  }
  if (existsSync(sourceUtoc)) {
    const companionDirectory = join(buildDirectory, 'companions');
    resetProjectDirectory(companionDirectory);
    const outputUtoc = join(companionDirectory, mode.patchName.replace(/\.pak$/i, '.utoc'));
    runExternal(retoc, iostoreCompanionArguments({
      patchPath: patch,
      outputUtoc,
      engineVersion: game.engineVersion,
    }), { capture: true });
    for (const extension of ['.utoc', '.ucas']) {
      const generated = outputUtoc.replace(/\.utoc$/i, extension);
      if (!existsSync(generated)) throw new Error(`retoc did not create IoStore companion: ${generated}`);
      const destination = patch.replace(/\.pak$/i, extension);
      copyFileSync(generated, destination);
      companionPaths.push(destination);
    }
  }
  if (game.requiresIoStoreCompanions && companionPaths.length !== 2) {
    throw new Error('IoStore build did not produce the required utoc and ucas companion pair');
  }
  const record = writeVerificationRecord({
    recordPath: join(mode.distDir, 'verification.json'),
    patchPath: patch,
    mode: mode.name,
    targetCulture: mode.targetCulture,
    virtualPaths,
    fileHashes,
    pak,
    companionPaths,
  });
  console.log(`${mode.name} patch built and verified: ${patch}`);
  console.log(`SHA-256: ${record.sha256}`);
}

function install() {
  const game = config();
  const modeName = requestedMode ?? game.defaultMode;
  const { repak } = tools();
  const result = activateLocalizationMode({
    game,
    modeName,
    verifyInstalled: ({ destination, verification }) => {
      const infoResult = runExternal(repak, ['info', destination], { capture: true });
      verifyRepakInfo(`${infoResult.stdout ?? ''}\n${infoResult.stderr ?? ''}`, {
        version: game.pakVersion,
        pathHashSeed: game.pakPathHashSeed,
        fileCount: verification.virtualPaths.length,
      });
      const listResult = runExternal(repak, ['list', destination], { capture: true });
      verifyRepakList(`${listResult.stdout ?? ''}\n${listResult.stderr ?? ''}`, verification.virtualPaths);
    },
  });
  const record = result.verification;
  for (const disabled of result.disabledPatches) console.log(`Disabled other mode: ${disabled}`);
  if (result.patchBackup) console.log(`Existing patch backed up to: ${result.patchBackup}`);
  if (result.configBackup) console.log(`User config backed up to: ${result.configBackup}`);
  console.log(`Activated ${modeName}: ${result.destination}`);
  console.log(`Installed SHA-256 verified: ${record.sha256}`);
  console.log(`Culture config: ${result.configPath}`);
}

function deactivate() {
  const game = config();
  const result = deactivateLocalizationModes({ game });
  for (const disabled of result.disabledPatches) console.log(`Disabled patch: ${disabled}`);
  if (result.configBackup) console.log(`User config backed up to: ${result.configBackup}`);
  console.log(`External localization patches disabled; culture restored to ${game.sourceCulture}`);
}

function help() {
  console.log('Usage: node src/cli.mjs <doctor|scan|extract|convert|build|install|deactivate> [game-id] [mode]');
}

try {
  if (command === 'doctor') doctor();
  else if (command === 'scan') scan();
  else if (command === 'extract') extract();
  else if (command === 'convert') await convert();
  else if (command === 'build') build();
  else if (command === 'install') install();
  else if (command === 'deactivate') deactivate();
  else help();
} catch (error) {
  console.error(error.stack || error.message);
  process.exitCode = 1;
}
