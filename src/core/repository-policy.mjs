const REQUIRED_DOCUMENTS = [
  'README.md',
  'LICENSE',
  'CONTRIBUTING.md',
  'SECURITY.md',
  'THIRD_PARTY_NOTICES.md',
];

const FORBIDDEN_PREFIXES = new Map([
  ['work/', 'local work data must stay untracked'],
  ['dist/', 'generated output must stay untracked'],
  ['tmp/', 'temporary output must stay untracked'],
  ['node_modules/', 'dependency installation must stay untracked'],
  ['.game-translate/', 'game-local runtime data must stay untracked'],
]);

const GAME_BINARY_EXTENSIONS = new Set([
  '.dll', '.exe', '.pak', '.utoc', '.ucas', '.locres', '.uasset', '.uexp', '.assets',
]);

function normalizePath(path) {
  return path.replaceAll('\\', '/').replace(/^\.\//, '');
}

function extension(path) {
  const slash = path.lastIndexOf('/');
  const dot = path.lastIndexOf('.');
  return dot > slash ? path.slice(dot).toLowerCase() : '';
}

export function auditRepositoryPaths(paths) {
  const issues = [];

  for (const originalPath of paths) {
    const path = normalizePath(originalPath);
    let reason;

    if (path.startsWith('.tools/') && path !== '.tools/manifest.json') {
      reason = 'downloaded tool payload must stay untracked';
    } else if (/(^|\/)[^/]*汉化文件[^/]*(\/|$)/u.test(path)) {
      reason = 'downloaded translation/game payload must stay untracked';
    } else {
      for (const [prefix, message] of FORBIDDEN_PREFIXES) {
        if (path.startsWith(prefix)) {
          reason = message;
          break;
        }
      }
    }

    if (!reason && !path.startsWith('test/fixtures/') && GAME_BINARY_EXTENSIONS.has(extension(path))) {
      reason = 'binary game payload must stay untracked';
    }

    if (reason) issues.push(`${path}: ${reason}`);
  }

  return issues.sort();
}

export function auditRequiredDocuments(paths) {
  const normalized = new Set(paths.map(normalizePath));
  return REQUIRED_DOCUMENTS
    .filter((path) => !normalized.has(path))
    .map((path) => `${path} is required`);
}

export function auditPublicRepository(paths) {
  return [
    ...auditRequiredDocuments(paths),
    ...auditRepositoryPaths(paths),
  ];
}
