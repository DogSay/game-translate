import { spawnSync } from 'node:child_process';
import { auditPublicRepository } from '../src/core/repository-policy.mjs';

const listed = spawnSync(
  'git',
  ['ls-files', '--cached', '--others', '--exclude-standard', '-z'],
  { encoding: 'utf8' },
);

if (listed.status !== 0) {
  console.error('Repository audit requires a Git worktree.');
  console.error(listed.stderr.trim());
  process.exit(1);
}

const paths = listed.stdout.split('\0').filter(Boolean);
const issues = auditPublicRepository(paths);

if (issues.length > 0) {
  console.error('Repository audit failed:');
  for (const issue of issues) console.error(`- ${issue}`);
  process.exit(1);
}

console.log(`Repository audit passed (${paths.length} public source files checked).`);
