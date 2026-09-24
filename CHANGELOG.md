# Changelog

All notable changes will be documented in this file. The project follows
semantic versioning once public releases begin.

## Unreleased

### Added

- Public repository policy audit and GitHub Actions CI.
- MIT project license, contribution guide, security policy, code of conduct,
  third-party notices, and release checklist.
- Machine-local `game.local.json` path override so public manifests do not need
  contributor-specific edits.

### Security

- Git rules exclude game archives, extracted localization, downloaded fan
  translations, generated output, secrets, native binaries, and local runtime
  state from source control.
- Public binary releases are explicitly blocked until the Oodle redistribution
  boundary is resolved.

## 0.1.0

- Initial Unreal locres v1-v3 surgery, external Pak/IoStore patch workflow,
  Taiwanese Traditional conversion, portable WinForms UI, and The Mound game
  manifest.
