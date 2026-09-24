import { copyFileSync, existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { basename, extname, join } from 'node:path';
import { parseCsv, stringifyCsv } from './localization-csv.mjs';

export function normalizeUEExtractorCsv(text, locresKeys) {
  const rows = parseCsv(text);
  const normalized = [];
  let repairs = 0;
  for (let index = 0; index < rows.length; index += 1) {
    const row = rows[index];
    const next = rows[index + 1];
    if (row.length >= 1 && row.slice(1).every((cell) => cell === '') && next?.length >= 2) {
      const combinedKey = `${row[0]}\n${next[0]}`;
      if (locresKeys.has(combinedKey)) {
        normalized.push([combinedKey, ...next.slice(1)]);
        repairs += 1;
        index += 1;
        continue;
      }
    }
    normalized.push(row);
  }
  if (repairs === 0) return { text, repairs };
  return {
    text: stringifyCsv(normalized, text.includes('\r\n') ? '\r\n' : '\n'),
    repairs,
  };
}

export function normalizeExtractedCsvFile(csvPath, hashesPath) {
  const hashes = JSON.parse(readFileSync(hashesPath, 'utf8').replace(/^\uFEFF/, ''));
  const result = normalizeUEExtractorCsv(readFileSync(csvPath, 'utf8'), new Set(Object.keys(hashes)));
  if (result.repairs > 0) writeFileSync(csvPath, result.text, 'utf8');
  return result.repairs;
}

export function promoteArchiveExtraction({ sourceDirectory, targetName, sourceArchive }) {
  const archiveBase = basename(sourceArchive, extname(sourceArchive));
  const variantCsv = join(sourceDirectory, `${targetName}_${archiveBase}.csv`);
  const variantHashes = join(sourceDirectory, `${targetName}_${archiveBase}.locreshashes`);
  if (!existsSync(variantCsv)) return false;
  copyFileSync(variantCsv, join(sourceDirectory, `${targetName}.csv`));
  const genericHashes = join(sourceDirectory, `${targetName}.locreshashes`);
  if (existsSync(variantHashes)) {
    copyFileSync(variantHashes, genericHashes);
  } else if (existsSync(genericHashes)) {
    rmSync(genericHashes);
  }
  return true;
}
