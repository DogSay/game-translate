import { parseCsv } from './localization-csv.mjs';

const MAGIC = Buffer.from([
  0x0E, 0x14, 0x74, 0x75, 0x67, 0x4A, 0x03, 0xFC,
  0x4A, 0x15, 0x90, 0x9D, 0xC3, 0x37, 0x7F, 0x1B,
]);

function requireBytes(buffer, offset, length, label) {
  if (!Number.isSafeInteger(offset) || !Number.isSafeInteger(length)
    || offset < 0 || length < 0 || offset + length > buffer.length) {
    throw new Error(`Invalid locres ${label}`);
  }
}

function readFString(buffer, start) {
  requireBytes(buffer, start, 4, 'FString length');
  const length = buffer.readInt32LE(start);
  let offset = start + 4;
  if (length === 0) return { value: '', offset };
  if (length === -0x80000000) throw new Error('Invalid locres FString length');
  if (length < 0) {
    const units = -length;
    requireBytes(buffer, offset, units * 2, 'UTF-16 FString');
    if (buffer.readUInt16LE(offset + (units - 1) * 2) !== 0) throw new Error('Invalid locres UTF-16 terminator');
    const value = buffer.toString('utf16le', offset, offset + (units - 1) * 2);
    return { value, offset: offset + units * 2 };
  }
  requireBytes(buffer, offset, length, 'ANSI FString');
  if (buffer[offset + length - 1] !== 0) throw new Error('Invalid locres ANSI terminator');
  const value = buffer.toString('utf8', offset, offset + length - 1);
  return { value, offset: offset + length };
}

function writeFString(value) {
  if (value.length === 0) return Buffer.alloc(4);
  if (/^[\x00-\x7F]*$/.test(value)) {
    const bytes = Buffer.from(value, 'ascii');
    const output = Buffer.alloc(4 + bytes.length + 1);
    output.writeInt32LE(bytes.length + 1);
    bytes.copy(output, 4);
    return output;
  }
  const bytes = Buffer.from(value, 'utf16le');
  const output = Buffer.alloc(4 + bytes.length + 2);
  output.writeInt32LE(-(bytes.length / 2 + 1));
  bytes.copy(output, 4);
  return output;
}

function replacementFor(value, translations) {
  if (translations.has(value)) return translations.get(value);
  const normalized = value.replaceAll('\r\n', '\n');
  if (translations.has(normalized)) return translations.get(normalized);
  const newline = value.includes('\r\n') ? '\r\n' : '\n';
  const cfKey = value.replace(/\r\n|\n/g, '<cf>');
  if (translations.has(cfKey)) return translations.get(cfKey).replaceAll('<cf>', newline);
  return value;
}

export function patchLocres(template, translations) {
  if (!Buffer.isBuffer(template)) throw new TypeError('Locres template must be a Buffer');
  if (!(translations instanceof Map)) throw new TypeError('Locres translations must be a Map');
  requireBytes(template, 0, 25, 'header');
  if (!template.subarray(0, MAGIC.length).equals(MAGIC)) throw new Error('Invalid locres magic');
  const version = template.readUInt8(16);
  if (version < 1 || version > 3) throw new Error(`Unsupported locres version: ${version}`);
  const offsetValue = template.readBigInt64LE(17);
  if (offsetValue < 25n || offsetValue > BigInt(Number.MAX_SAFE_INTEGER)) throw new Error('Invalid locres string array offset');
  const arrayOffset = Number(offsetValue);
  requireBytes(template, arrayOffset, 4, 'string array count');
  const count = template.readInt32LE(arrayOffset);
  if (count < 0) throw new Error('Invalid locres string array count');

  let offset = arrayOffset + 4;
  const items = [];
  for (let index = 0; index < count; index += 1) {
    const string = readFString(template, offset);
    offset = string.offset;
    let referenceCount = null;
    if (version >= 2) {
      requireBytes(template, offset, 4, 'string reference count');
      referenceCount = template.readInt32LE(offset);
      offset += 4;
    }
    items.push({ value: string.value, referenceCount });
  }
  if (offset !== template.length) {
    throw new Error(`Locres string array does not end at EOF (${offset} != ${template.length})`);
  }

  const parts = [template.subarray(0, arrayOffset + 4)];
  let replaced = 0;
  for (const item of items) {
    const value = replacementFor(item.value, translations);
    if (value !== item.value) replaced += 1;
    parts.push(writeFString(value));
    if (item.referenceCount !== null) {
      const bytes = Buffer.alloc(4);
      bytes.writeInt32LE(item.referenceCount);
      parts.push(bytes);
    }
  }
  return { bytes: Buffer.concat(parts), version, stringCount: count, replaced };
}

export function translationsFromCsv(csvText, memory = {}) {
  const rows = parseCsv(csvText);
  const headers = rows[0] ?? [];
  const normalized = headers.map((value) => value.replace(/^\uFEFF/, '').trim().toLowerCase());
  const sourceIndex = normalized.indexOf('source');
  const translationIndex = normalized.indexOf('translation');
  if (sourceIndex < 0 || translationIndex < 0) {
    throw new Error('Localization CSV requires source and Translation columns');
  }
  const result = new Map();
  for (const row of rows.slice(1)) {
    const source = row[sourceIndex] ?? '';
    const translation = row[translationIndex] ?? '';
    if (source && translation && source !== translation && !result.has(source)) result.set(source, translation);
    const normalizedSource = source.replaceAll('\r\n', '\n');
    if (normalizedSource && translation && normalizedSource !== source && !result.has(normalizedSource)) {
      result.set(normalizedSource, translation);
    }
  }
  for (const [source, translation] of Object.entries(memory ?? {})) {
    if (!result.has(source)) result.set(source, translation);
  }
  return result;
}

const EXACT_OVERRIDES = new Map([
  // The compat patch replaces the zh-Hans slot, so a faithful character
  // conversion is not enough: the language selector must identify its content.
  ['简体中文', '繁體中文'],
]);

export function applyLocresOverrides(translations) {
  if (!(translations instanceof Map)) throw new TypeError('Locres translations must be a Map');
  const result = new Map(translations);
  for (const [source, translation] of EXACT_OVERRIDES) result.set(source, translation);
  return result;
}

export function patchTranslatedLocres(template, csvText, memory = {}, { compat = false } = {}) {
  const translations = translationsFromCsv(csvText, memory);
  return patchLocres(template, compat ? applyLocresOverrides(translations) : translations);
}
