import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';

export function loadToolPaths(projectRoot) {
  const manifest = JSON.parse(readFileSync(join(projectRoot, '.tools', 'manifest.json'), 'utf8'));
  return Object.fromEntries(
    Object.entries(manifest).map(([name, details]) => [name, join(projectRoot, details.path)]),
  );
}

export function runExternal(executable, args, options = {}) {
  const result = spawnSync(executable, args, {
    cwd: options.cwd,
    encoding: 'utf8',
    stdio: options.capture ? 'pipe' : 'inherit',
    windowsHide: true,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) {
    const details = options.capture ? `\n${result.stdout ?? ''}${result.stderr ?? ''}` : '';
    throw new Error(`${executable} exited with code ${result.status}${details}`);
  }
  return result;
}
