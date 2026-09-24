import { appendFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname } from 'node:path';

export function translationEntriesFromDocument(payload) {
  if (payload === null || typeof payload !== 'object' || Array.isArray(payload)) {
    throw new TypeError('Translation-memory snapshot must be an object');
  }
  let entries = payload;
  if (Object.hasOwn(payload, 'version') && typeof payload.version === 'number') {
    if (!Number.isInteger(payload.version) || payload.version !== 1) {
      throw new Error(`Unsupported translation-memory version: ${payload.version}`);
    }
    if (!Object.hasOwn(payload, 'entries')) {
      throw new Error('Translation-memory v1 snapshot requires entries');
    }
    entries = payload.entries;
  }
  if (entries === null || typeof entries !== 'object' || Array.isArray(entries)) {
    throw new TypeError('Translation-memory entries must be an object');
  }
  const result = {};
  for (const [source, translation] of Object.entries(entries)) {
    if (typeof translation !== 'string') {
      throw new TypeError(`Translation-memory value for ${JSON.stringify(source)} must be a string`);
    }
    result[source] = translation;
  }
  return result;
}

export class TranslationMemory {
  constructor(path) {
    this.path = path;
    this.journalPath = `${path}.journal`;
    this.entries = new Map();
    this.inFlight = new Map();
    this.writeChain = Promise.resolve();
    if (existsSync(path)) {
      const payload = JSON.parse(readFileSync(path, 'utf8'));
      const entries = translationEntriesFromDocument(payload);
      this.entries = new Map(Object.entries(entries));
    }
    if (existsSync(this.journalPath)) {
      const lines = readFileSync(this.journalPath, 'utf8').split(/\r?\n/);
      for (let index = 0; index < lines.length; index += 1) {
        const line = lines[index].trim();
        if (line === '') continue;
        try {
          const entry = JSON.parse(line);
          if (!Array.isArray(entry) || entry.length !== 2) throw new Error('invalid entry');
          this.entries.set(entry[0], entry[1]);
        } catch (error) {
          const hasLaterContent = lines.slice(index + 1).some((candidate) => candidate.trim() !== '');
          if (hasLaterContent) throw new Error(`Corrupt translation-memory journal at line ${index + 1}`, { cause: error });
        }
      }
    }
  }

  get(source) {
    return this.entries.get(source);
  }

  async convert(source, provider) {
    if (this.entries.has(source)) return this.entries.get(source);
    if (this.inFlight.has(source)) return this.inFlight.get(source);

    const pending = (async () => {
      const translated = await provider(source);
      this.entries.set(source, translated);
      await this.#save(source, translated);
      return translated;
    })();
    this.inFlight.set(source, pending);
    try {
      return await pending;
    } finally {
      this.inFlight.delete(source);
    }
  }

  async convertBatch(sources, provider) {
    if (!Array.isArray(sources)) throw new TypeError('Translation-memory batch must be an array');
    const missing = [...new Set(sources.filter((source) => !this.entries.has(source)))];
    if (missing.length > 0) {
      const translated = await provider(missing);
      if (!Array.isArray(translated) || translated.length !== missing.length
        || translated.some((value) => typeof value !== 'string')) {
        throw new Error('Translation provider returned an invalid batch');
      }
      for (let index = 0; index < missing.length; index += 1) {
        this.entries.set(missing[index], translated[index]);
        await this.#save(missing[index], translated[index]);
      }
    }
    return sources.map((source) => this.entries.get(source));
  }

  #save(source, translated) {
    this.writeChain = this.writeChain.then(() => {
      mkdirSync(dirname(this.path), { recursive: true });
      appendFileSync(this.journalPath, `${JSON.stringify([source, translated])}\n`, {
        encoding: 'utf8',
        flush: true,
      });
    });
    return this.writeChain;
  }
}
