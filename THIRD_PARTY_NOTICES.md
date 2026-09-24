# Third-party notices

The MIT license in `LICENSE` covers Game Translate's own source code only.
Third-party tools, services, games, fonts, translations, and native libraries
remain subject to their own terms.

## Referenced tools

| Component | Pinned version | Upstream | Upstream license |
| --- | --- | --- | --- |
| UEExtractor | 1.0.8.4 | <https://github.com/SolicenTEAM/UEExtractor> | MIT |
| repak | 0.2.3 | <https://github.com/trumank/repak> | Apache-2.0 OR MIT |
| retoc | 0.1.5 | <https://github.com/trumank/retoc> | MIT |
| CUE4Parse (transitive through UEExtractor) | upstream dependency | <https://github.com/FabianFG/CUE4Parse> | Apache-2.0 |
| Zlib-ng.NET native helper | local runtime dependency | <https://github.com/NotOfficer/Zlib-ng.NET> | MIT |

The source repository stores only download metadata and checksums for these
tools. Their downloaded payloads under `.tools/` are deliberately untracked.
Any distributor must include the license texts required by the exact binaries
they ship.

## Oodle

Oodle technology and `oo2core_*` binaries are proprietary and are not covered
by this project's MIT license. They must not be committed to this repository or
published in a Game Translate release without separate redistribution rights.
Where a workflow requires Oodle, the user must provide a legally obtained copy
from their own game or engine installation. Public binary releases remain
blocked until the build no longer embeds an unlicensed Oodle payload.

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
