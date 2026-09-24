import { protectedTokens } from '../core/protected-text.mjs';
import { parseCsv } from '../formats/localization-csv.mjs';

function headerIndex(headers, name) {
  return headers.findIndex((header) => (
    header.replace(/^\uFEFF/, '').trim().toLowerCase() === name.toLowerCase()
  ));
}

function dataRows(text) {
  const parsed = parseCsv(text);
  return {
    headers: parsed[0] ?? [],
    rows: parsed.slice(1).filter((row) => !row.every((cell) => cell === '')),
  };
}

export function validateTranslatedCsv(sourceText, translatedText, targetName) {
  const source = dataRows(sourceText);
  const translated = dataRows(translatedText);
  if (source.rows.length !== translated.rows.length) {
    throw new Error(`${targetName}: source/translated row count differs (${source.rows.length}/${translated.rows.length})`);
  }

  const sourceKey = headerIndex(source.headers, 'key');
  const sourceTextIndex = headerIndex(source.headers, 'source');
  const translatedKey = headerIndex(translated.headers, 'key');
  const translatedSource = headerIndex(translated.headers, 'source');
  const translation = headerIndex(translated.headers, 'translation');
  if ([sourceKey, sourceTextIndex, translatedKey, translatedSource, translation].includes(-1)) {
    throw new Error(`${targetName}: localization CSV headers are incomplete`);
  }

  for (let index = 0; index < source.rows.length; index += 1) {
    const sourceRow = source.rows[index];
    const translatedRow = translated.rows[index];
    const rowNumber = index + 1;
    if (sourceRow[sourceKey] !== translatedRow[translatedKey]
      || sourceRow[sourceTextIndex] !== translatedRow[translatedSource]) {
      throw new Error(`${targetName}: key/source identity mismatch at row ${rowNumber}`);
    }
    const value = translatedRow[translation] ?? '';
    if (value === '') throw new Error(`${targetName}: blank translation at row ${rowNumber}`);
    if (value.includes('\uFFFD')) throw new Error(`${targetName}: replacement character at row ${rowNumber}`);
    const expectedTokens = protectedTokens(sourceRow[sourceTextIndex]);
    const actualTokens = protectedTokens(value);
    if (JSON.stringify(expectedTokens) !== JSON.stringify(actualTokens)) {
      throw new Error(`${targetName}: protected token mismatch at row ${rowNumber}`);
    }
  }
  return { entries: source.rows.length };
}
