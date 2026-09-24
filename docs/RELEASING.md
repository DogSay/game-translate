# Release checklist

This checklist separates publishing the source repository from distributing a
portable binary.

## Source release

1. Run `npm test`.
2. Run `npm run audit:repo`.
3. Review `git status --short --ignored` for game data, local paths, secrets,
   translation memory, and generated output.
4. Confirm every game claim distinguishes artifact validation from a fresh
   in-game visual check.
5. Tag only a clean commit reviewed on GitHub.

## Portable binary release

In addition to the source-release checks:

1. Resolve the Oodle redistribution gate documented in
   `THIRD_PARTY_NOTICES.md`. Do not publish the current local executable while
   it embeds an Oodle binary without separate permission.
2. Include all required third-party license texts.
3. Build from a clean checkout with pinned dependencies.
4. Run the portable tool startup, API, archive, and rollback checks.
5. Test the single executable from a representative game root.
6. Record the executable SHA-256 and supported game/engine matrix.
7. Require a fresh in-game visual check for every adapter changed by the
   release.

GitHub source availability does not grant permission to redistribute games,
fan translations, fonts, or proprietary compression libraries.
