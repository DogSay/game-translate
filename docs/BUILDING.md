# Build from source (Windows)

The public checkout is immediately testable, but the portable EXE is **not**
currently reproducible from source alone. It embeds third-party executables and
native libraries that are not committed to Git. In particular, Oodle binaries
are proprietary. A successful local build is not permission to redistribute
its output. See [third-party notices](../THIRD_PARTY_NOTICES.md) and the
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
| `.tools/UEExtractor-1.0.8.4/UEExtractor.dll` | Pinned UEExtractor archive |
| `.tools/repak-0.2.3/repak.exe` and `LICENSE-APACHE`, `LICENSE-MIT` | Pinned repak archive |
| `.tools/repak-0.2.3/oo2core_9_win64.dll` | A legally obtained Oodle copy compatible with this repak build |
| `.tools/retoc-0.1.5/retoc.exe` and `LICENSE` | Pinned retoc archive |
| `oodle-data-shared.dll` | A compatible, legally obtained local runtime; verify its provenance before use |
| `zlib-ng2.dll` | Zlib-ng.NET native runtime; the current local build uses version 2.3.2 |

The archive hashes in the manifest do **not** verify a separately supplied
Oodle or zlib DLL. Do not download native DLLs from an untrusted DLL mirror.
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

1. Remove the unlicensed Oodle payload from the EXE, or obtain explicit
   redistribution permission.
2. Pin and verify every distributable build input from a clean checkout,
   including the native zlib runtime and required license texts.
3. Re-run startup, archive, rollback, and fresh in-game checks on the resulting
   binary. A green source CI run is not a game-verification result.
