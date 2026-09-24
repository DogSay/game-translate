# Architecture

Game Translate separates reusable localization logic from game-specific facts
and from generated or copyrighted data.

## Layers

- `src/core/` contains engine detection, protected-text handling, manifest
  loading, translation memory, and repository safety policy.
- `src/formats/` parses and validates localization formats such as CSV and
  Unreal locres.
- `src/adapters/` maps engine-specific paths and packaging arguments.
- `src/workflows/` composes conversion, verification, and reversible patch
  lifecycle operations.
- `desktop/GameTranslate.Core/` implements the portable .NET workflow.
- `desktop/GameTranslate.App/` is the Windows UI and embedded-tool host.
- `games/` contains portable interoperability facts, never extracted game
  content.
- `work/` and `dist/` contain ignored local inputs and generated artifacts.

## Trust boundaries

```text
game archives (read-only)
        |
        v
extract/probe -> protected conversion -> structural validation
        |                                  |
        +------------> staging ------------+
                          |
                          v
              independently named patch
                          |
                          v
              hash + round-trip verification
                          |
                          v
              transactional install/disable
```

Original game archives are read-only. A successful translation is not enough
to authorize installation: the output must also match its manifest, preserve
protected structure, survive package round-trip checks, and remain removable
without restoring an original archive.

## Source and local configuration

`games/<id>/game.json` records portable game facts. A contributor can copy
`game.local.example.json` to ignored `game.local.json` and change only
`gamePath`. Engine, packaging, culture, and virtual-path facts cannot be
overridden locally.

## Verification language

The project distinguishes three states:

1. **Implemented**: source and tests exist.
2. **Artifact-verified**: structure and hashes pass automated checks.
3. **Game-verified**: the exact current artifact has passed a fresh in-game
   visual check.

Documentation must not collapse these states into a single claim.
