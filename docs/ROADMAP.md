# Roadmap

This is a status guide, not a promise that an untested game will work.

## Next release gates

1. **License-cleared portable build.** Verify the no-Oodle build and first-run
   download terms, reproduce pinned dependencies, and build/test from a clean checkout.
   Publish an EXE only after the [release checklist](RELEASING.md) passes.
2. **Fresh Unreal game check.** Re-check the current The Mound pipeline in-game,
   including translated UI, fonts, clipping, and the disable/restore path. Keep
   this separate from automated artifact verification.
3. **First Unity adapter.** Choose a representative Unity game with an
   externally deployable localization format; add extraction, conversion,
   installation, rollback, and an in-game check before claiming Unity support.
4. **Contributor experience.** Add a current UI demonstration after the next
   verified build, and collect issue reports for unsupported layouts without
   asking contributors to upload copyrighted game data.

## Current boundaries

- Unreal locres v1-v3 patch generation and Pak/IoStore companion validation are
  implemented. Each new game still needs layout and runtime verification.
- Unity packaging detection is implemented; Unity translation is not.
- No public EXE is available. The source can be tested without game assets,
  while local EXE builds need the inputs described in [Build from source](BUILDING.md).
- Native `zh-Hant` mode is not offered for The Mound because its runtime and
  language selector do not support it. Compatibility mode overrides `zh-Hans`.
