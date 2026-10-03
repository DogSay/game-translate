# Contributing to Game Translate

Thanks for helping make reusable, reversible game-localization tooling.

## Development setup

- Windows 10 or later
- Node.js 20 or later
- .NET SDK 10

Run the complete test suite before opening a pull request:

```powershell
npm test
npm run audit:repo
```

The source tests do not require a commercial game installation. Building the
portable executable additionally requires the pinned local tools described by
`.tools/manifest.json`; downloaded binaries remain untracked. Follow
[Build from source](docs/BUILDING.md) for the exact local inputs and the
redistribution restriction.

For a checked-in game manifest, copy its `game.local.example.json` to
`game.local.json` and set your own installation path. The local file is ignored
and may override only `gamePath`.

## Safety rules

- Never commit game archives, extracted game content, fan-translation packages,
  encryption keys, player configuration, logs, backups, or translation memory.
- Prefer additive patches or mods. Do not modify a game's original archives.
- Preserve placeholders, markup, keys, hashes, namespaces, and row order.
- Add a failing test before changing behavior, then run the full suite.
- Do not describe a game adapter as verified until it has passed a fresh
  in-game visual check.
- Keep third-party tools and native libraries out of Git. Pin their origin and
  checksum instead.

## Adding a game

Put reusable facts in `games/<game-id>/`, implementation in the appropriate
engine adapter, and generated data in ignored `work/` and `dist/` directories.
Document the engine, packaging, source and target cultures, exact external
artifact paths, validation evidence, and rollback procedure.

Do not submit copyrighted game data as a fixture. Small fixtures must be
synthetic and belong under `test/fixtures/`.

## Pull requests

Keep changes focused. Explain the failure mode, the external-patch or rollback
boundary, tests run, and whether an in-game check is still outstanding.
