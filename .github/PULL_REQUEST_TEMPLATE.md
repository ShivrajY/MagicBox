## Summary

<!-- What problem does this change solve? -->

## Validation

- [ ] `dotnet restore --locked-mode`
- [ ] `dotnet build MagicBox.sln --no-restore --configuration Release`
- [ ] `dotnet test MagicBox.sln --no-restore --configuration Release`
- [ ] Fixture coverage added or updated for parser/adapter behavior

## Safety and compatibility

- [ ] No credentials, cookies, private data, or unredacted response bodies are committed.
- [ ] URL, redirect, pagination, cancellation, and secret-redaction behavior was considered.
- [ ] README or adapter documentation was updated when public behavior changed.
- [ ] Known limitations are described in the PR body.
