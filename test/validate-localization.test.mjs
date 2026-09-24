import assert from 'node:assert/strict';
import { test } from 'node:test';

async function loadModule() {
  return import('../src/workflows/validate-localization.mjs').catch(() => ({}));
}

const source = 'key,source,Translation\nA,\u7b80\u4f53 {Count},\nB,\u53e6\u4e00\u689d,\n';
const valid = 'key,source,Translation\nA,\u7b80\u4f53 {Count},\u7e41\u9ad4 {Count}\nB,\u53e6\u4e00\u689d,\u53e6\u4e00\u689d\n';

test('validates translated rows against the immutable source CSV', async () => {
  const { validateTranslatedCsv } = await loadModule();
  assert.equal(typeof validateTranslatedCsv, 'function');
  assert.deepEqual(validateTranslatedCsv(source, valid, 'Game'), { entries: 2 });
});

test('rejects a translated CSV with a dropped row', async () => {
  const { validateTranslatedCsv } = await loadModule();
  const dropped = 'key,source,Translation\nA,\u7b80\u4f53 {Count},\u7e41\u9ad4 {Count}\n';
  assert.throws(() => validateTranslatedCsv(source, dropped, 'Game'), /Game.*row count/i);
});

test('rejects changed key or source identity', async () => {
  const { validateTranslatedCsv } = await loadModule();
  const changed = valid.replace('B,\u53e6\u4e00\u689d', 'Changed,\u53e6\u4e00\u689d');
  assert.throws(() => validateTranslatedCsv(source, changed, 'Game'), /Game.*identity.*row 2/i);
});

test('rejects blank or replacement-character translations', async () => {
  const { validateTranslatedCsv } = await loadModule();
  assert.throws(
    () => validateTranslatedCsv(source, valid.replace('\u53e6\u4e00\u689d\n', '\n'), 'Game'),
    /Game.*blank.*row 2/i,
  );
  assert.throws(
    () => validateTranslatedCsv(source, valid.replace('\u53e6\u4e00\u689d\n', '\uFFFD\n'), 'Game'),
    /Game.*replacement.*row 2/i,
  );
});

test('rejects a translation that removes protected tokens', async () => {
  const { validateTranslatedCsv } = await loadModule();
  const changed = valid.replace('\u7e41\u9ad4 {Count}', '\u7e41\u9ad4');
  assert.throws(() => validateTranslatedCsv(source, changed, 'Game'), /Game.*protected token.*row 1/i);
});
