const DEFAULT_ENDPOINT = 'https://api.zhconvert.org/convert';

export class ZhConvertClient {
  constructor({
    fetchImpl = globalThis.fetch,
    endpoint = DEFAULT_ENDPOINT,
    timeoutMs = 30_000,
    maxAttempts = 6,
    retryDelay = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds)),
  } = {}) {
    if (typeof fetchImpl !== 'function') throw new TypeError('A fetch implementation is required');
    this.fetchImpl = fetchImpl;
    this.endpoint = endpoint;
    this.timeoutMs = timeoutMs;
    this.maxAttempts = maxAttempts;
    this.retryDelay = retryDelay;
    this.cache = new Map();
  }

  async convert(text) {
    if (this.cache.has(text)) return this.cache.get(text);

    const pending = this.#requestWithRetry(text);
    this.cache.set(text, pending);
    try {
      return await pending;
    } catch (error) {
      this.cache.delete(text);
      throw error;
    }
  }

  async convertBatch(texts) {
    if (!Array.isArray(texts)) throw new TypeError('Batch text must be an array');
    if (texts.length === 0) return [];
    if (texts.some((text) => typeof text !== 'string')) {
      throw new TypeError('Every batch item must be a string');
    }
    const converted = await this.#requestWithRetry(JSON.stringify(texts));
    let values;
    try {
      values = JSON.parse(converted);
    } catch (error) {
      throw new Error('zhconvert batch response was not valid JSON', { cause: error });
    }
    if (!Array.isArray(values) || values.length !== texts.length || values.some((value) => typeof value !== 'string')) {
      throw new Error('zhconvert batch response did not match the submitted text array');
    }
    return values;
  }

  async #requestWithRetry(text) {
    for (let attempt = 1; attempt <= this.maxAttempts; attempt += 1) {
      try {
        return await this.#request(text);
      } catch (error) {
        if (!error.transient || attempt === this.maxAttempts) throw error;
        const fallbackDelay = Math.min(30_000, 2_000 * (2 ** (attempt - 1)));
        await this.retryDelay(error.retryAfterMs ?? fallbackDelay);
      }
    }
    throw new Error('zhconvert retry loop ended unexpectedly');
  }

  async #request(text) {
    const body = new URLSearchParams({ converter: 'Taiwan', text }).toString();
    const response = await this.fetchImpl(this.endpoint, {
      method: 'POST',
      headers: { 'content-type': 'application/x-www-form-urlencoded; charset=UTF-8' },
      body,
      signal: AbortSignal.timeout(this.timeoutMs),
    });

    if (!response.ok) {
      const error = new Error(`zhconvert HTTP ${response.status ?? 'error'}`);
      error.transient = response.status === 429 || response.status >= 500;
      const retryAfter = response.headers?.get?.('retry-after');
      if (retryAfter && /^\d+(?:\.\d+)?$/.test(retryAfter.trim())) {
        error.retryAfterMs = Math.ceil(Number(retryAfter) * 1_000);
      } else if (retryAfter) {
        const retryAt = Date.parse(retryAfter);
        if (Number.isFinite(retryAt)) error.retryAfterMs = Math.max(0, retryAt - Date.now());
      }
      throw error;
    }
    const payload = await response.json();
    if (payload.code !== 0) throw new Error(`zhconvert API error: ${payload.msg || payload.code}`);
    if (typeof payload.data?.text !== 'string') throw new Error('zhconvert returned no converted text');
    return payload.data.text;
  }
}
