import { existsSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { basename, dirname, join } from 'node:path';
import { randomUUID } from 'node:crypto';
import { convertLocalizationCsv } from '../formats/localization-csv.mjs';

export async function convertCsvFile({
  inputPath,
  outputPath,
  converter,
  concurrency = 1,
  batchConverter,
  batchSize,
}) {
  const source = readFileSync(inputPath, 'utf8');
  const result = await convertLocalizationCsv(source, converter, {
    concurrency,
    batchConverter,
    batchSize,
  });
  const temporaryPath = join(
    dirname(outputPath),
    `.${basename(outputPath)}.${process.pid}.${randomUUID()}.tmp`,
  );

  try {
    writeFileSync(temporaryPath, result.text, { encoding: 'utf8', flush: true });
    renameSync(temporaryPath, outputPath);
  } finally {
    if (existsSync(temporaryPath)) rmSync(temporaryPath, { force: true });
  }

  return result.stats;
}
