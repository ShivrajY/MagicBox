# MagicBox architecture

MagicBox separates website-specific behavior from execution infrastructure.
Adapters describe requests, rendering, extraction, transforms, and pagination;
the runtime owns cancellation, retries, pacing, validation, output, and
diagnostics.

## Execution flow

1. `IAdapterRegistry` loads YAML adapters and normalizes them to JSON.
2. The registry validates the normalized document against
   `schemas/adapter-v1.schema.json` before any network request.
3. `ScrapeOrchestrator` validates inputs and creates a bounded run.
4. The fetch router selects HTTP or the optional browser implementation from
   the adapter's render policy.
5. `DocumentExtractor` parses HTML with AngleSharp or JSON with `JsonPath.Net`.
6. Only allowlisted transforms are applied to field values.
7. Required-field policy and per-run de-duplication decide whether a record is
   emitted or rejected.
8. `ScrapeEvent` values stream to a sink or caller as pages complete.

No adapter can load an assembly, execute shell commands, evaluate arbitrary
JavaScript, or bypass the extraction pipeline.

## Replaceable boundaries

The public interfaces in `MagicBox.Core` keep infrastructure replaceable:

- `IFetcher` — HTTP or browser document acquisition;
- `IScrapeScheduler` — bounded local scheduling and a future distributed
  implementation;
- `IAdapterRegistry` — adapter discovery and validation;
- `ISecretStore` and `ISessionStore` — local encrypted state or a future
  external provider;
- `IChallengeHandler` — explicit human checkpoints, never automated solving;
- `IResultSink` — JSONL, CSV, or a future durable sink;
- `IRobotsReporter` — report-only robots diagnostics.

The first release intentionally provides no database, durable queue,
checkpoint/resume system, or distributed worker implementation.

## Safety model

The default profile is designed for a developer laptop: bounded concurrency,
per-host pacing, cancellation propagation, a response-body limit, redirect
validation, and private-network blocking. High-volume mode increases limits
only when the caller requests it; it does not grant permission to access a
site.

Secrets use an authenticated AES-256-GCM envelope with a random salt and
nonce, and a PBKDF2-HMAC-SHA256-derived key. Adapter files contain logical
secret names rather than credentials. Diagnostics must pass through the
redaction boundary before reaching a sink or terminal.

## Browser boundary

`MagicBox.Browser` is optional so HTTP-only users do not need browser binaries.
Browser-rendered pages are serialized into the same HTML extraction path as
HTTP responses, avoiding two separate selector dialects. Login actions are
restricted to navigation, fill, click, press, select, waits, assertions, and
manual challenge checkpoints.
