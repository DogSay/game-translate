# Security Policy

## Supported versions

Security fixes are made on the latest revision of the default branch. Until
the project publishes versioned releases, older local builds are unsupported.

## Reporting a vulnerability

Please use the repository's private GitHub security-advisory feature. Do not
open a public issue for vulnerabilities involving path traversal, archive
extraction, command execution, update/download integrity, secrets, or unsafe
mod installation.

Include reproduction steps, affected paths or game layouts, expected impact,
and a proposed mitigation if available. Do not attach copyrighted game files,
API keys, encryption keys, or personal save/configuration data.

## Security boundaries

Game Translate treats game archives and downloaded tools as untrusted input.
Install operations must remain limited to tool-owned external patch names and
must never overwrite original game archives. Checksums establish artifact
identity; they do not establish that a third-party binary is trustworthy or
licensed for redistribution.
