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

1. Verify the exact EXE contains no Oodle payload, and review the tools'
   first-run Oodle download behavior and terms as documented in
   `THIRD_PARTY_NOTICES.md`. Do not assume attribution or a dependency's
   open-source license grants rights to redistribute Oodle.
   Verify an existing mismatched Oodle DLL is rejected before either tool runs,
   and a fresh download matches the pinned archive and DLL SHA-256 values.
2. Include all required third-party license texts.
3. Build from a clean checkout with pinned dependencies.
4. Run the portable tool self-test, API, archive, and rollback checks. The
   `--tools-test` command verifies UEExtractor's installed files and native
   runtime but does not launch UEExtractor without a game input; use a real
   read-only game extraction for that startup check.
5. Test the single executable from a representative game root.
6. Record the executable SHA-256 and supported game/engine matrix.
7. Require a fresh in-game visual check for every adapter changed by the
   release.

GitHub source availability does not grant permission to redistribute games,
fan translations, fonts, or proprietary compression libraries.
