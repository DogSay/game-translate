# Build from source (Windows)

The public checkout is immediately testable, but the portable EXE is **not**
currently reproducible from source alone. It embeds third-party executables and
a native zlib library that are not committed to Git. Oodle binaries are not
embedded. A successful local build is not permission to redistribute its
output. See [third-party notices](../THIRD_PARTY_NOTICES.md) and the
[release gate](RELEASING.md).

## Source-only verification

Requirements: Windows, Node.js 20+, and the .NET 10 SDK. From a fresh checkout:

```powershell
npm test
npm run audit:repo
```

The tests use synthetic fixtures and require no commercial game or API key.
GitHub Actions runs these commands; it does not publish an EXE.

## Inputs for a local EXE build

Download the exact upstream release archives listed in
[`.tools/manifest.json`](../.tools/manifest.json). Verify each **downloaded
archive** against its manifest `sha256` before extraction:

```powershell
(Get-FileHash -Algorithm SHA256 -LiteralPath 'C:\path\to\download.zip').Hash
```

Extract each archive into the matching ignored directory, without changing its
contents. These files must exist before `npm run publish:exe`:

| Local input | Source |
| --- | --- |
| `.tools/UEExtractor-1.0.8.4/UEExtractor.exe`, `UEExtractor.dll`, and `UEExtractor.runtimeconfig.json` | Pinned UEExtractor archive |
| `.tools/repak-0.2.3/repak.exe` and `LICENSE-APACHE`, `LICENSE-MIT` | Pinned repak archive |
| `.tools/retoc-0.1.5/retoc.exe` and `LICENSE` | Pinned retoc archive |
| `zlib-ng2.dll` | `bin/zlib-ng2.dll` from the pinned zlib-ng 2.3.2 archive |

For zlib-ng, verify both the archive `sha256` and the extracted `fileSha256`
from the manifest. The corresponding license texts are tracked under
`licenses/` and embedded in the portable EXE. Do not download native DLLs from
an untrusted DLL mirror.
The project does not yet provide a one-command, license-cleared dependency
bootstrap. Until that exists, maintainers must inspect the exact local inputs
and license texts themselves.

Once the inputs are present, run:

```powershell
npm run publish:exe
```

This creates an ignored `dist/portable/GameTranslate.exe` for **local testing
only**. Before using it with a game, follow the [portable checks in the release
checklist](RELEASING.md). Never commit the EXE, native DLLs, downloaded tools,
game files, translation memory, or generated patches.

## What still blocks a public EXE

1. Independently verify the no-Oodle EXE and review the tools' first-run
   Oodle download behavior and terms before public distribution.
2. Reproduce the pinned distributable build inputs from a clean checkout and
   review the bundled license texts against those exact binaries.
3. Re-run startup, archive, rollback, and fresh in-game checks on the resulting
   binary. A green source CI run is not a game-verification result.
