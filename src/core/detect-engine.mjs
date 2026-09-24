import { existsSync, readdirSync, statSync } from 'node:fs';
import { basename, join } from 'node:path';

const UNKNOWN = Object.freeze({
  engine: 'unknown',
  packaging: 'unknown',
  project: null,
});

function directoriesIn(root) {
  return readdirSync(root, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name);
}

function filesIn(root) {
  return readdirSync(root, { withFileTypes: true })
    .filter((entry) => entry.isFile())
    .map((entry) => entry.name);
}

export function detectEngine(root) {
  if (!existsSync(root) || !statSync(root).isDirectory()) return { ...UNKNOWN };

  for (const project of directoriesIn(root)) {
    const paks = join(root, project, 'Content', 'Paks');
    if (!existsSync(paks)) continue;
    const archiveNames = filesIn(paks);
    if (archiveNames.some((name) => name.toLowerCase().endsWith('.utoc'))) {
      return { engine: 'unreal', packaging: 'iostore', project };
    }
    if (archiveNames.some((name) => name.toLowerCase().endsWith('.pak'))) {
      return { engine: 'unreal', packaging: 'pak', project };
    }
  }

  if (existsSync(join(root, 'UnityPlayer.dll'))) {
    const dataDirectory = directoriesIn(root).find((name) => name.endsWith('_Data'));
    if (dataDirectory && existsSync(join(root, dataDirectory, 'globalgamemanagers'))) {
      return {
        engine: 'unity',
        packaging: 'assets',
        project: basename(dataDirectory, '_Data'),
      };
    }
  }

  return { ...UNKNOWN };
}
