# Adapter authoring guide

Adapters are declarative YAML files with `schemaVersion: 1`. Website behavior
belongs in an adapter; runtime behavior belongs in `MagicBox.Core`.

## Start from a fixture

1. Capture a representative HTML or JSON response that you are authorized to
   use for testing.
2. Save a sanitized copy under `tests/fixtures/`.
3. Add the adapter under `adapters/` with a unique stable ID.
4. Validate it before any live request:

   ```bash
   dotnet run --project src/MagicBox.Cli -- adapter validate path/to/adapter.yaml
   ```

5. Add a deterministic test that maps the fixture to canonical fields.
6. Run the full locked restore, build, and test commands before opening a PR.

Do not commit credentials, session cookies, private customer data, or
unredacted pages containing personal information.

## Required shape

```yaml
schemaVersion: 1
id: example
kind: general
inputs:
  query:
    required: true
    type: string
request:
  method: GET
  url: https://example.test/search
  query:
    q: ${input.query}
render:
  mode: http
response:
  type: html
extract:
  items:
    css: article.result
  fields:
    title:
      source: text
      selector: h2
      transforms: [trim]
    url:
      source: attribute
      selector: a
      attribute: href
      transforms:
        - requireAbsoluteUri: true
pagination:
  strategy: none
policy:
  requiredFields: [title, url]
```

Search adapters use `kind: search` and must emit `title` and `url`. The
registry rejects unsupported schema versions, duplicate IDs, missing required
inputs, unsupported URI schemes, and arbitrary execution fields.

## Design rules

- Prefer stable selectors and explicit required fields.
- Keep pagination bounded with `MaxPages` and `MaxRecords`.
- Use `auto` rendering only with an explicit, reviewable fallback condition.
- Use logical secret names; never place a password or token in YAML.
- Resolve extracted links against the source URL and require HTTP or HTTPS.
- Add fixture coverage before changing a built-in adapter.
- Respect authorization, terms, rate limits, privacy, and applicable law.
