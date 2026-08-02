# Contributing to MagicBox

Thank you for helping improve MagicBox. The project values small, reviewable
changes with deterministic tests and clear safety behavior.

## Development setup

Install the .NET 10 SDK selected by `global.json`, then run:

```bash
dotnet restore --locked-mode
dotnet build MagicBox.sln --no-restore --configuration Release
dotnet test MagicBox.sln --no-restore --configuration Release
```

The default suite is offline and fixture-based. Do not make tests depend on a
live search engine or website. Browser tests must remain isolated from the
HTTP-only test path and must install a pinned Playwright browser in CI.

## Changes and pull requests

- Explain the user-facing problem and the smallest useful change.
- Add or update deterministic fixtures for parser or adapter behavior.
- Add tests for safety boundaries, failure paths, and new public contracts.
- Keep credentials, cookies, private customer data, and personal data out of
  commits and fixtures.
- Update the README or a guide when CLI behavior, adapter schema, or public API
  changes.
- Run the locked restore, Release build, and test commands before requesting
  review.

## Adapter contributions

Read [docs/adapter-authoring.md](docs/adapter-authoring.md) first. An adapter
must use the versioned schema and allowlisted transforms; arbitrary scripts,
shell commands, dynamic assemblies, and browser evaluation are not accepted.

## Review expectations

Reviewers look for bounded work, cancellation propagation, safe URL handling,
secret redaction, deterministic output, and an honest explanation of known
limitations. A passing build is necessary but does not replace a security or
policy review.

Contributions are expected to be compatible with the license selected for the
repository.
