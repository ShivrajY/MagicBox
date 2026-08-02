# MagicBox Universal Scraper

MagicBox is a .NET 10 asynchronous scraping framework. Website behavior lives
in validated YAML adapters; the runtime provides HTTP fetching, HTML/CSS and
JSONPath extraction, bounded scheduling, report-only robots diagnostics,
allowlisted transforms, streaming output, optional Playwright Chromium
rendering, and an encrypted local secret store.

The solution contains:

- `MagicBox.Core`: public CLR-friendly contracts and the extraction pipeline.
- `MagicBox.Browser`: optional Playwright renderer and configured login actions.
- `MagicBox.Cli`: the `magicbox` command-line tool.
- `MagicBox.Tests`: deterministic offline fixture and security tests.

The six migrated search adapters are in `adapters/builtin/`: Google, Yahoo,
Bing, AOL, DuckDuckGo, and Brave.

## Build and test

The repository pins the .NET 10 SDK in `global.json`, centralizes NuGet
versions in `Directory.Packages.props`, and commits package lock files.

```bash
dotnet restore --locked-mode
dotnet build MagicBox.sln --no-restore
dotnet test MagicBox.sln --no-restore
```

The browser project is optional at runtime. After building, install the
versioned Chromium binary with the Playwright script emitted beside the CLI:

```bash
pwsh src/MagicBox.Cli/bin/Debug/net10.0/playwright.ps1 install chromium
```

## CLI examples

```bash
dotnet run --project src/MagicBox.Cli -- adapter list
dotnet run --project src/MagicBox.Cli -- adapter validate adapters/builtin
dotnet run --project src/MagicBox.Cli -- scrape duckduckgo --input query=dotnet --records 10
dotnet run --project src/MagicBox.Cli -- search dotnet --engines google,bing --records 10
dotnet run --project src/MagicBox.Cli -- doctor --browser
```

Records are written incrementally as JSONL by default. Use `--format csv` and
`--output results.csv` for CSV output. Diagnostics and summaries go to stderr.
The `local` profile uses conservative concurrency and pacing; `high-volume`
raises bounded scheduler limits but does not bypass website authorization,
terms, rate limits, or applicable law.

## Adapter contract

Every adapter has `schemaVersion: 1`, a unique ID, an HTTP/HTTPS request,
response type (`html` or `json`), selector extraction, allowlisted transforms,
pagination policy, and required-field policy. YAML cannot contain F#, shell,
JavaScript, dynamic assemblies, or browser evaluation code. Validation happens
before the first fetch.

Search adapters must emit `title` and an absolute `url`; `snippet` is optional.
General website adapters can emit arbitrary schema-defined fields. The public
library surface is asynchronous:

```csharp
await foreach (var item in orchestrator.RunAsync(request, cancellationToken))
{
    if (item.Kind == ScrapeEventKind.Record)
    {
        Console.WriteLine(item.Record!.Fields);
    }
}
```

## Secrets and policy

`magicbox secrets init|set|list|remove|rotate` manages an AES-256-GCM text
envelope. Keys are derived from a passphrase with PBKDF2-HMAC-SHA256 and a
600,000-iteration work factor. Plaintext credentials and session state are not
stored in adapter YAML or source control. An environment passphrase is
supported for unattended runs with a warning.

Robots.txt is report-only: MagicBox reports the path decision but does not use
it as an authorization bypass or an automatic block. Users remain responsible
for authorization, site terms, rate limits, privacy, and applicable law.

The v1 boundary intentionally excludes databases, checkpoints, durable queues,
distributed workers, arbitrary adapter code, automatic CAPTCHA/MFA solving,
commercial proxy-provider integrations, and document/OCR extraction.
