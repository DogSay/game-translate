const CJK = /\p{Script=Han}/u;
const PROTECTED_TOKEN = /(<[^>]*>|\{[^{}\r\n]*\}|%(?:\d+\$)?[-+0 #]*\d*(?:\.\d+)?[A-Za-z]|\\(?:[nrt\\]|u[0-9A-Fa-f]{4})|\[\/?[A-Za-z][^\]\r\n]*\])/g;

export function hasCjk(text) {
  return CJK.test(text);
}

export function protectedTokens(text) {
  return [...String(text ?? '').matchAll(PROTECTED_TOKEN)].map((match) => match[0]);
}

export async function convertProtectedText(text, converter) {
  let result = '';
  let position = 0;

  for (const match of text.matchAll(PROTECTED_TOKEN)) {
    const visible = text.slice(position, match.index);
    result += hasCjk(visible) ? await converter(visible) : visible;
    result += match[0];
    position = match.index + match[0].length;
  }

  const tail = text.slice(position);
  result += hasCjk(tail) ? await converter(tail) : tail;
  return result;
}

export async function convertProtectedTexts(texts, batchConverter) {
  const visibleSegments = [];
  const layouts = texts.map((text) => {
    const parts = [];
    let position = 0;
    for (const match of text.matchAll(PROTECTED_TOKEN)) {
      const visible = text.slice(position, match.index);
      if (hasCjk(visible)) {
        parts.push({ convertedIndex: visibleSegments.length });
        visibleSegments.push(visible);
      } else {
        parts.push(visible);
      }
      parts.push(match[0]);
      position = match.index + match[0].length;
    }
    const tail = text.slice(position);
    if (hasCjk(tail)) {
      parts.push({ convertedIndex: visibleSegments.length });
      visibleSegments.push(tail);
    } else {
      parts.push(tail);
    }
    return parts;
  });

  const convertedSegments = visibleSegments.length > 0
    ? await batchConverter(visibleSegments)
    : [];
  if (!Array.isArray(convertedSegments) || convertedSegments.length !== visibleSegments.length) {
    throw new Error('Batch converter returned a different number of visible text segments');
  }
  if (convertedSegments.some((value) => typeof value !== 'string')) {
    throw new Error('Batch converter returned a non-string text segment');
  }

  return layouts.map((parts) => parts.map((part) => (
    typeof part === 'string' ? part : convertedSegments[part.convertedIndex]
  )).join(''));
}
