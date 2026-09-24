import { convertProtectedText, convertProtectedTexts } from '../core/protected-text.mjs';

export function parseCsv(text) {
  const rows = [];
  let row = [];
  let field = '';
  let quoted = false;

  for (let index = 0; index < text.length; index += 1) {
    const character = text[index];
    if (quoted) {
      if (character === '"' && text[index + 1] === '"') {
        field += '"';
        index += 1;
      } else if (character === '"') {
        quoted = false;
      } else {
        field += character;
      }
      continue;
    }

    if (character === '"' && field.length === 0) {
      quoted = true;
    } else if (character === ',') {
      row.push(field);
      field = '';
    } else if (character === '\r' || character === '\n') {
      if (character === '\r' && text[index + 1] === '\n') index += 1;
      row.push(field);
      rows.push(row);
      row = [];
      field = '';
    } else {
      field += character;
    }
  }

  if (quoted) throw new Error('Malformed CSV: unterminated quoted field');
  if (field.length > 0 || row.length > 0) {
    row.push(field);
    rows.push(row);
  }
  return rows;
}

function escapeCsvField(value) {
  const text = String(value ?? '');
  return /[",\r\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
}

export function stringifyCsv(rows, eol = '\n') {
  if (rows.length === 0) return '';
  return `${rows.map((row) => row.map(escapeCsvField).join(',')).join(eol)}${eol}`;
}

function headerIndex(headers, expected) {
  return headers.findIndex((header) => header.replace(/^\uFEFF/, '').trim().toLowerCase() === expected);
}

export async function convertLocalizationCsv(
  text,
  converter,
  { concurrency = 1, batchConverter, batchSize = 100 } = {},
) {
  const eol = text.includes('\r\n') ? '\r\n' : '\n';
  const rows = parseCsv(text);
  const headers = rows[0] ?? [];
  const sourceIndex = headerIndex(headers, 'source');
  const translationIndex = headerIndex(headers, 'translation');
  if (sourceIndex < 0 || translationIndex < 0) {
    throw new Error('Localization CSV requires source and Translation columns');
  }

  const stats = { rows: 0, converted: 0, preserved: 0 };
  const pendingRows = [];
  for (const row of rows.slice(1)) {
    if (row.every((cell) => cell === '')) continue;
    stats.rows += 1;
    while (row.length <= translationIndex) row.push('');
    if (row[translationIndex] !== '') {
      stats.preserved += 1;
      continue;
    }
    pendingRows.push(row);
  }

  if (batchConverter) {
    const size = Math.max(1, Number(batchSize) || 1);
    for (let start = 0; start < pendingRows.length; start += size) {
      const rows = pendingRows.slice(start, start + size);
      const converted = await convertProtectedTexts(
        rows.map((row) => row[sourceIndex] ?? ''),
        batchConverter,
      );
      for (let index = 0; index < rows.length; index += 1) {
        rows[index][translationIndex] = converted[index];
        stats.converted += 1;
      }
    }
  } else {
    let cursor = 0;
    async function worker() {
      while (cursor < pendingRows.length) {
        const row = pendingRows[cursor];
        cursor += 1;
        row[translationIndex] = await convertProtectedText(row[sourceIndex] ?? '', converter);
        stats.converted += 1;
      }
    }
    const workerCount = Math.max(1, Math.min(Number(concurrency) || 1, pendingRows.length || 1));
    await Promise.all(Array.from({ length: workerCount }, () => worker()));
  }

  return { text: stringifyCsv(rows, eol), stats };
}
