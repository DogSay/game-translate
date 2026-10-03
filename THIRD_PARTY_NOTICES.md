# Third-party notices

The MIT license in `LICENSE` covers Game Translate's own source code only.
Third-party tools, services, games, fonts, translations, and native libraries
remain subject to their own terms.

## Referenced tools

| Component | Pinned version | Upstream | Upstream license |
| --- | --- | --- | --- |
| UEExtractor | 1.0.8.4 | <https://github.com/SolicenTEAM/UEExtractor> | MIT; [license text](licenses/UEExtractor-MIT.txt) |
| repak | 0.2.3 | <https://github.com/trumank/repak> | Apache-2.0 OR MIT |
| retoc | 0.1.5 | <https://github.com/trumank/retoc> | MIT |
| CUE4Parse (transitive through UEExtractor) | upstream dependency | <https://github.com/FabianFG/CUE4Parse> | Apache-2.0; [license text](licenses/CUE4Parse-APACHE.txt) |
| zlib-ng native runtime | 2.3.2 | <https://github.com/zlib-ng/zlib-ng> | zlib; [license text](licenses/zlib-ng-LICENSE.txt) |

The source repository stores download metadata, checksums, and license texts.
Downloaded binaries under `.tools/` are deliberately untracked. The portable
build embeds the applicable license texts beside the extracted tools. A
distributor must still check the licenses against the exact binaries shipped.

## Oodle

Oodle technology and `oo2core_*` binaries are proprietary and are not covered
by this project's MIT license. They must not be committed to this repository or
published in a Game Translate release without separate redistribution rights.
The local no-Oodle build does not embed these DLLs. At translation time, the
application obtains a pinned `oodle-data-shared.dll` archive over HTTPS,
verifies both archive and DLL SHA-256, and installs it under
`.game-translate/tools/`. One known legacy UEExtractor download is replaced
only after the new archive and DLL pass verification; other mismatched DLLs
are rejected.
For repak's `oo2core_9_win64.dll`, the application checks any pre-existing DLL
against the pinned hash; repak verifies its own first-time download. This
requires network access on a fresh installation. Runtime download behavior
does not grant Game Translate redistribution rights. A public binary release
still requires review of the exact artifact, dependency terms, and first-run
behavior under the [release checklist](docs/RELEASING.md).

## Translation service

Game Translate can call the third-party zhconvert service at
<https://zhconvert.org/>. Commercial usage may require a paid plan. API use is
governed by the service's current terms and is not granted by this repository's
license.

## Games, mods, translations, and fonts

No original game archive, extracted game content, encryption key, downloaded
fan translation, or third-party font is part of the open-source project.
Manifests and documentation record interoperability facts only. Users are
responsible for obtaining games and optional translation inputs lawfully.
