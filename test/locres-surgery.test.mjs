import assert from 'node:assert/strict';
import { test } from 'node:test';

const MAGIC = Buffer.from([
  0x0E, 0x14, 0x74, 0x75, 0x67, 0x4A, 0x03, 0xFC,
  0x4A, 0x15, 0x90, 0x9D, 0xC3, 0x37, 0x7F, 0x1B,
]);

function fstring(value) {
  if (/^[\x00-\x7F]*$/.test(value)) {
    const text = Buffer.from(value, 'ascii');
    const output = Buffer.alloc(4 + text.length + 1);
    output.writeInt32LE(text.length + 1);
    text.copy(output, 4);
    return output;
  }
  const text = Buffer.from(value, 'utf16le');
  const output = Buffer.alloc(4 + text.length + 2);
  output.writeInt32LE(-(text.length / 2 + 1));
  text.copy(output, 4);
  return output;
}

function fixture(version, strings) {
  const tableBytes = Buffer.from('namespace-key-and-cityhash-table', 'ascii');
  const arrayOffset = 25 + tableBytes.length;
  const header = Buffer.alloc(arrayOffset + 4);
  MAGIC.copy(header);
  header.writeUInt8(version, 16);
  header.writeBigInt64LE(BigInt(arrayOffset), 17);
  tableBytes.copy(header, 25);
  header.writeInt32LE(strings.length, arrayOffset);
  const items = [];
  for (let index = 0; index < strings.length; index += 1) {
    items.push(fstring(strings[index]));
    if (version >= 2) {
      const refs = Buffer.alloc(4);
      refs.writeInt32LE(index + 7);
      items.push(refs);
    }
  }
  return { bytes: Buffer.concat([header, ...items]), prefixLength: arrayOffset + 4 };
}

async function api() {
  return import('../src/formats/locres-surgery.mjs').catch(() => ({}));
}

test('preserves a v3 locres header and hash tables byte-for-byte', async () => {
  const { patchLocres } = await api();
  assert.equal(typeof patchLocres, 'function');
  const input = fixture(3, ['继续战斗', 'No change']);

  const result = patchLocres(input.bytes, new Map([['继续战斗', '繼續戰鬥']]));

  assert.equal(result.version, 3);
  assert.deepEqual(result.bytes.subarray(0, input.prefixLength), input.bytes.subarray(0, input.prefixLength));
  assert.equal(result.replaced, 1);
  assert.notEqual(result.bytes.indexOf(Buffer.from('繼續戰鬥', 'utf16le')), -1);
});

test('auto-detects and preserves the v1 string-array layout', async () => {
  const { patchLocres } = await api();
  const input = fixture(1, ['战斗']);

  const result = patchLocres(input.bytes, new Map([['战斗', '戰鬥']]));

  assert.equal(result.version, 1);
  assert.deepEqual(result.bytes.subarray(0, input.prefixLength), input.bytes.subarray(0, input.prefixLength));
  assert.notEqual(result.bytes.indexOf(Buffer.from('戰鬥', 'utf16le')), -1);
});

test('preserves v2 reference counts while replacing strings', async () => {
  const { patchLocres } = await api();
  const input = fixture(2, ['战斗']);
  const result = patchLocres(input.bytes, new Map([['战斗', '戰鬥']]));
  assert.equal(result.version, 2);
  assert.deepEqual(result.bytes.subarray(-4), input.bytes.subarray(-4));
});

test('rejects a locres whose localized string array does not end at EOF', async () => {
  const { patchLocres } = await api();
  const input = fixture(3, ['战斗']);
  const corrupt = Buffer.concat([input.bytes, Buffer.from([0xAA])]);

  assert.throws(() => patchLocres(corrupt, new Map()), /string array.*EOF/i);
});

test('supports UEExtractor cf newline lookup while preserving the original newline style', async () => {
  const { patchLocres } = await api();
  const input = fixture(3, ['第一行\r\n继续']);

  const result = patchLocres(input.bytes, new Map([['第一行<cf>继续', '第一行<cf>繼續']]));

  assert.notEqual(result.bytes.indexOf(Buffer.from('第一行\r\n繼續', 'utf16le')), -1);
});

test('builds a source to Translation map from translated CSV and optional memory', async () => {
  const { translationsFromCsv } = await api();
  const map = translationsFromCsv(
    'key,source,Translation\r\na,继续游戏,繼續遊戲\r\nb,战斗,战斗\r\n',
    { 战斗: '戰鬥', 退出: '退出' },
  );
  assert.equal(map.get('继续游戏'), '繼續遊戲');
  assert.equal(map.get('战斗'), '戰鬥');
  assert.equal(map.get('退出'), '退出');
});

test('compat override renames the Simplified Chinese language entry to Traditional Chinese', async () => {
  const { applyLocresOverrides } = await api();
  assert.equal(typeof applyLocresOverrides, 'function');

  const converted = new Map([
    ['简体中文', '簡體中文'],
    ['战斗', '戰鬥'],
  ]);
  const result = applyLocresOverrides(converted);

  assert.equal(result.get('简体中文'), '繁體中文');
  assert.equal(result.get('战斗'), '戰鬥');
});

test('build policy applies the language-label override only in compat mode', async () => {
  const { patchTranslatedLocres } = await api();
  assert.equal(typeof patchTranslatedLocres, 'function');
  const input = fixture(3, ['简体中文']);
  const csv = 'key,source,Translation\r\na,简体中文,簡體中文\r\n';

  const compat = patchTranslatedLocres(input.bytes, csv, {}, { compat: true });
  const native = patchTranslatedLocres(input.bytes, csv, {}, { compat: false });

  assert.notEqual(compat.bytes.indexOf(Buffer.from('繁體中文', 'utf16le')), -1);
  assert.notEqual(native.bytes.indexOf(Buffer.from('簡體中文', 'utf16le')), -1);
  assert.equal(native.bytes.indexOf(Buffer.from('繁體中文', 'utf16le')), -1);
});
